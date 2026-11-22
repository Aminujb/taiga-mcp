using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ModelContextProtocol.Protocol;
using Prometheus;
using TaigaMcp.Configuration;
using TaigaMcp.Health;
using TaigaMcp.Hosting;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Security;
using TaigaMcp.Taiga;

// ---------------------------------------------------------------------------
// Entry point of the Taiga MCP server.
//
// MCP is served over HTTP using the Streamable HTTP transport (ModelContextProtocol.AspNetCore).
// The connection point is the application root ("/").
//
// Taiga settings come from the "Taiga" section (appsettings.json), the optional
// appsettings.Local.json (for local secrets) and environment variables:
//   TAIGA_URL, TAIGA_USERNAME, TAIGA_PASSWORD, TAIGA_TOKEN, TAIGA_REFRESH_TOKEN,
//   TAIGA_AUTH_METHOD, TAIGA_AUTH_SCHEME, TAIGA_COOKIE, TAIGA_CA_CERT_PATH,
//   TAIGA_IGNORE_SSL_ERRORS, TAIGA_TIMEOUT_SECONDS, TAIGA_MAX_RETRIES, TAIGA_RETRY_DELAY_MS.
// Listen address and port: the "Server" section or MCP_HOST / MCP_PORT.
// Audit of destructive tools: the "Audit" section or AUDIT_ENABLED / AUDIT_FILE_PATH,
//   AUDIT_MAX_FILE_SIZE_MB (default 64) / AUDIT_MAX_BACKUP_FILES.
// MCP endpoint protection: Mcp:AuthToken or the MCP_AUTH_TOKEN environment variable.
// Health endpoints: /health, /health/live, /health/ready;
//   readiness cache: the "Health" section or HEALTH_READY_CACHE_SECONDS.
// Prometheus metrics: the "Metrics" section or METRICS_ENABLED / METRICS_PATH.
// Rate limiting: the "RateLimit" section or RATE_LIMIT_*.
// Graceful shutdown: the "Shutdown" section or SHUTDOWN_*.
// ---------------------------------------------------------------------------

// Settings are always looked up next to the build output, not in the current directory,
// so the MCP server works when started from any working directory.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Local secrets file (never in the repository); takes priority over appsettings.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

// 0. Web server settings: listen address and port (the "Server" section).
//    Listens on 127.0.0.1 by default; set 0.0.0.0 to listen on all interfaces.
var serverOptions = new ServerOptions();
builder.Configuration.GetSection("Server").Bind(serverOptions);
serverOptions.Host = ReadEnv("MCP_HOST") ?? serverOptions.Host;
if (int.TryParse(ReadEnv("MCP_PORT"), out var configuredPort))
{
    serverOptions.Port = configuredPort;
}

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(ResolveHost(serverOptions.Host), serverOptions.Port));

// 1. Taiga connection settings.
var options = new TaigaOptions();
builder.Configuration.GetSection("Taiga").Bind(options);

// Override with environment variables (when set).
options.BaseUrl = ReadEnv("TAIGA_URL") ?? options.BaseUrl;
options.Username = ReadEnv("TAIGA_USERNAME") ?? options.Username;
options.Password = ReadEnv("TAIGA_PASSWORD") ?? options.Password;
options.Token = ReadEnv("TAIGA_TOKEN") ?? options.Token;
options.RefreshToken = ReadEnv("TAIGA_REFRESH_TOKEN") ?? options.RefreshToken;
options.AuthMethod = ReadEnv("TAIGA_AUTH_METHOD") ?? options.AuthMethod;
options.AuthScheme = ReadEnv("TAIGA_AUTH_SCHEME") ?? options.AuthScheme;
if (int.TryParse(ReadEnv("TAIGA_TIMEOUT_SECONDS"), out var timeoutSeconds))
{
    options.TimeoutSeconds = timeoutSeconds;
}
options.Cookie = ReadEnv("TAIGA_COOKIE") ?? options.Cookie;
options.CaCertificatePath = ReadEnv("TAIGA_CA_CERT_PATH") ?? options.CaCertificatePath;
if (bool.TryParse(ReadEnv("TAIGA_IGNORE_SSL_ERRORS"), out var ignoreSsl))
{
    options.IgnoreSslErrors = ignoreSsl;
}
if (int.TryParse(ReadEnv("TAIGA_MAX_RETRIES"), out var maxRetries))
{
    options.MaxRetries = maxRetries;
}
if (int.TryParse(ReadEnv("TAIGA_RETRY_DELAY_MS"), out var retryDelay))
{
    options.RetryBaseDelayMs = retryDelay;
}

// Early validation of a corporate certificate: a configuration error
// (missing, corrupt or expired) is caught at startup, not on the first request.
TaigaHttpHandlerFactory.ValidateConfiguration(options);

builder.Services.AddSingleton(options);

// The Taiga token cache must be a singleton, or every tool call would log in again.
builder.Services.AddSingleton<TaigaAuthStore>();

// Audit of destructive tools: settings and a singleton logger.
var auditOptions = new AuditOptions();
builder.Configuration.GetSection("Audit").Bind(auditOptions);
if (bool.TryParse(ReadEnv("AUDIT_ENABLED"), out var auditEnabled))
{
    auditOptions.Enabled = auditEnabled;
}
auditOptions.FilePath = ReadEnv("AUDIT_FILE_PATH") ?? auditOptions.FilePath;
if (int.TryParse(ReadEnv("AUDIT_MAX_FILE_SIZE_MB"), out var auditMaxSize))
{
    auditOptions.MaxFileSizeMB = auditMaxSize;
}
if (int.TryParse(ReadEnv("AUDIT_MAX_BACKUP_FILES"), out var auditMaxBackups))
{
    auditOptions.MaxBackupFiles = auditMaxBackups;
}

builder.Services.AddSingleton(auditOptions);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditLogger>();

// Health checks: readiness is cached so Taiga isn't polled on every request.
var healthOptions = new HealthOptions();
builder.Configuration.GetSection("Health").Bind(healthOptions);
if (int.TryParse(ReadEnv("HEALTH_READY_CACHE_SECONDS"), out var healthReadyCache))
{
    healthOptions.ReadyCacheSeconds = healthReadyCache;
}

builder.Services.AddSingleton(healthOptions);
builder.Services.AddSingleton<ReadinessProbe>();

// Prometheus metrics (RPS, latency and error rate of tool calls).
var metricsOptions = new MetricsOptions();
builder.Configuration.GetSection("Metrics").Bind(metricsOptions);
if (bool.TryParse(ReadEnv("METRICS_ENABLED"), out var metricsEnabled))
{
    metricsOptions.Enabled = metricsEnabled;
}
metricsOptions.Path = ReadEnv("METRICS_PATH") ?? metricsOptions.Path;
if (!metricsOptions.Path.StartsWith('/'))
{
    metricsOptions.Path = "/" + metricsOptions.Path;
}

builder.Services.AddSingleton<McpMetrics>();

// Rate limiting for the MCP endpoint (partitioned by token or IP).
var rateLimitOptions = new RateLimitOptions();
builder.Configuration.GetSection("RateLimit").Bind(rateLimitOptions);
if (bool.TryParse(ReadEnv("RATE_LIMIT_ENABLED"), out var rateLimitEnabled))
{
    rateLimitOptions.Enabled = rateLimitEnabled;
}
if (int.TryParse(ReadEnv("RATE_LIMIT_PERMIT"), out var rateLimitPermit))
{
    rateLimitOptions.PermitLimit = rateLimitPermit;
}
if (int.TryParse(ReadEnv("RATE_LIMIT_WINDOW_SECONDS"), out var rateLimitWindow))
{
    rateLimitOptions.WindowSeconds = rateLimitWindow;
}

if (rateLimitOptions.Enabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global limiter: protects the MCP endpoint and skips health/metrics.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/health") ||
                (metricsOptions.Enabled && path.StartsWithSegments(metricsOptions.Path)))
            {
                return RateLimitPartition.GetNoLimiter("unprotected");
            }

            return RateLimitSetup.CreatePartition(context, rateLimitOptions);
        });

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.Headers.RetryAfter =
                rateLimitOptions.WindowSeconds.ToString(CultureInfo.InvariantCulture);
            if (!context.HttpContext.Response.HasStarted)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync("Too Many Requests", token);
            }
        };
    });
}

// Graceful shutdown and handling of critical errors.
var shutdownOptions = new ShutdownOptions();
builder.Configuration.GetSection("Shutdown").Bind(shutdownOptions);
if (int.TryParse(ReadEnv("SHUTDOWN_TIMEOUT_SECONDS"), out var shutdownTimeout))
{
    shutdownOptions.ShutdownTimeoutSeconds = shutdownTimeout;
}
if (bool.TryParse(ReadEnv("SHUTDOWN_ON_UNHANDLED_EXCEPTION"), out var shutdownOnUnhandled))
{
    shutdownOptions.ShutdownOnUnhandledException = shutdownOnUnhandled;
}
builder.Services.Configure<HostOptions>(
    host => host.ShutdownTimeout = TimeSpan.FromSeconds(Math.Max(1, shutdownOptions.ShutdownTimeoutSeconds)));

// 2. Typed HttpClient for the Taiga API.
//    Corporate and self-signed certificates are handled in TaigaHttpHandlerFactory
//    (settings were validated at startup). IHttpClientFactory manages the handler's
//    lifecycle, so HttpClient doesn't need to be disposed manually.
builder.Services
    .AddHttpClient<TaigaClient>()
    .ConfigurePrimaryHttpMessageHandler(() => TaigaHttpHandlerFactory.Create(options))
    .SetHandlerLifetime(TimeSpan.FromMinutes(2));

// 3. MCP server: HTTP transport plus all tools in this assembly.
builder.Services
    .AddMcpServer(mcp =>
    {
        mcp.ServerInfo = new Implementation
        {
            Name = "taiga-mcp",
            Title = "Taiga MCP Server",
            Version = "1.0.0",
        };
        mcp.ServerInstructions =
            "This server provides tools for managing Taiga: projects, user stories, " +
            "tasks, issues, epics, roles, users, and memberships/invitations. " +
            "When changing objects, use the version parameter (OCC: optimistic concurrency control).";
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// 3.1. Graceful shutdown (the timeout is set through HostOptions.ShutdownTimeout).
{
    var shutdownLogger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger(GracefulShutdown.LoggerCategory);
    var applicationLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    GracefulShutdown.Register(shutdownLogger, applicationLifetime, shutdownOptions);
}

// 3.2. Prometheus HTTP metrics and rate limiting.
if (metricsOptions.Enabled)
{
    app.UseHttpMetrics();
    // Create the metrics up front so the series appear in /metrics from startup.
    app.Services.GetRequiredService<McpMetrics>();
}

if (rateLimitOptions.Enabled)
{
    app.UseRateLimiter();
}

// 4. Protect the MCP endpoint with a shared token (X-Api-Key or Authorization: Bearer header).
//    Health checks stay open. Without a token, access isn't restricted (local use only).
var mcpAuthToken = builder.Configuration["Mcp:AuthToken"];
if (string.IsNullOrWhiteSpace(mcpAuthToken))
{
    mcpAuthToken = ReadEnv("MCP_AUTH_TOKEN");
}

if (string.IsNullOrWhiteSpace(mcpAuthToken))
{
    app.Logger.LogWarning(
        "MCP endpoint is not protected: Mcp:AuthToken / MCP_AUTH_TOKEN is not set. " +
        "The server is listening on {Host}:{Port}: when bound to 0.0.0.0 the endpoint is reachable from the network " +
        "without authentication. Setting a token is strongly recommended.",
        serverOptions.Host,
        serverOptions.Port);
}
else
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") ||
            (metricsOptions.Enabled && path.StartsWithSegments(metricsOptions.Path)))
        {
            await next();
            return;
        }

        if (!IsAuthorized(context.Request, mcpAuthToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next();
    });
}

// Health check endpoints (not part of MCP): /health, /health/live, /health/ready.
app.MapHealthEndpoints();

// Prometheus metrics (RPS, latency, error rate).
if (metricsOptions.Enabled)
{
    app.MapMetrics(metricsOptions.Path);
}

// Main MCP endpoint (Streamable HTTP).
app.MapMcp();

app.Run();

// Helper that reads an environment variable.
static string? ReadEnv(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

// Converts the configured address string to an IPAddress.
static IPAddress ResolveHost(string host)
{
    if (string.IsNullOrWhiteSpace(host) || host is "0.0.0.0" or "*" or "+")
    {
        return IPAddress.Any;
    }

    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return IPAddress.Loopback;
    }

    return IPAddress.Parse(host);
}

// Checks the MCP access token, protected against timing attacks.
static bool IsAuthorized(HttpRequest request, string expected)
{
    string? provided = null;

    if (request.Headers.TryGetValue("X-Api-Key", out var apiKey))
    {
        provided = apiKey.ToString();
    }
    else if (request.Headers.TryGetValue("Authorization", out var auth) &&
             auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        provided = auth.ToString()["Bearer ".Length..].Trim();
    }

    if (string.IsNullOrEmpty(provided))
    {
        return false;
    }

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(provided),
        Encoding.UTF8.GetBytes(expected));
}
