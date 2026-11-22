using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TaigaMcp.Configuration;

namespace TaigaMcp.Taiga;

/// <summary>
/// Thin wrapper over the Taiga REST API.
///
/// Provides:
///  - standard token authentication (Authorization: Bearer ... header);
///  - automatic username/password login and token refresh on 401;
///  - OCC (Optimistic Concurrency Control): the version parameter is passed in tools;
///  - pagination control (the x-disable-pagination header).
/// </summary>
public sealed class TaigaClient
{
    private readonly HttpClient _http;
    private readonly TaigaOptions _options;
    private readonly TaigaAuthStore _authStore;

    // JSON serialization settings: nulls are not written and non-ASCII text is not escaped.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public TaigaClient(HttpClient http, TaigaOptions options, TaigaAuthStore authStore)
    {
        _http = http;
        _options = options;
        _authStore = authStore;

        // Normalize the base address: remove spaces and the trailing slash, then add
        // exactly one slash, which correct relative paths require.
        var baseUrl = (options.BaseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
        _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ---------------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------------

    /// <summary>SSO cookie session mode (the Authorization header is not used).</summary>
    private bool IsCookieMode()
        => string.Equals(_options.AuthMethod?.Trim(), "cookie", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns a valid token, logging in or refreshing if needed.</summary>
    private async Task<string> GetTokenAsync(bool force, CancellationToken ct)
    {
                var method = string.IsNullOrWhiteSpace(_options.AuthMethod) ? "auto" : _options.AuthMethod.Trim().ToLowerInvariant();

        // Read the token once: the property is thread-safe, but a second read
        // could return a different value between the check and the return.
        var cached = _authStore.AuthToken;
        if (!force && cached is not null)
        {
            return cached;
        }

        await _authStore.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = _authStore.AuthToken;
            if (!force && current is not null)
            {
                return current;
            }

            // SSO option: only the token supplied in advance is used.
            if (method == "token")
            {
                if (!string.IsNullOrWhiteSpace(_authStore.AuthToken))
                {
                    return _authStore.AuthToken!;
                }

                throw new InvalidOperationException(
                    "AuthMethod=token requires Token to be set (the auth_token from the current Taiga session).");
            }

            // "password" mode: always log in with username and password.
            if (method == "password")
            {
                await LoginAsync(ct).ConfigureAwait(false);
                return _authStore.AuthToken!;
            }

            // Automatic mode: refresh, then username and password.
            // 1) Try to refresh the token with the refresh token.
            if (!string.IsNullOrWhiteSpace(_authStore.RefreshToken))
            {
                try
                {
                    await RefreshTokenAsync(ct).ConfigureAwait(false);
                    return _authStore.AuthToken!;
                }
                catch (TaigaApiException)
                {
                    // Refresh failed: fall through to a normal login.
                }
            }

            // 2) Normal username/password login.
            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await LoginAsync(ct).ConfigureAwait(false);
                return _authStore.AuthToken!;
            }

            throw new InvalidOperationException(
                "Taiga credentials are not set. Provide Token or Username/Password in the configuration.");
        }
        finally
        {
            _authStore.Gate.Release();
        }
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["type"] = "normal",
            ["username"] = _options.Username,
            ["password"] = _options.Password,
        };

        var (status, text, _) = await RawAsync(HttpMethod.Post, "auth", body, null, false, null, ct).ConfigureAwait(false);
        if (!IsSuccess(status))
        {
            throw new TaigaApiException(status, text);
        }

        var node = ParseAuthResponse(status, text, "authentication");
        _authStore.AuthToken = ReadAuthToken(node, status, "authentication");

        if (node["refresh"] is JsonValue refreshValue &&
            refreshValue.TryGetValue<string>(out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            _authStore.RefreshToken = refreshToken;
        }
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        var body = new JsonObject { ["refresh"] = _authStore.RefreshToken };
        var (status, text, _) = await RawAsync(HttpMethod.Post, "auth/refresh", body, null, false, null, ct).ConfigureAwait(false);
        if (!IsSuccess(status))
        {
            throw new TaigaApiException(status, text);
        }

        var node = ParseAuthResponse(status, text, "token refresh");
        _authStore.AuthToken = ReadAuthToken(node, status, "token refresh");
    }

    /// <summary>
    /// Parses a JSON authentication response. Invalid JSON must not cause an
    /// unhandled exception, so it is converted to a <see cref="TaigaApiException"/>.
    /// </summary>
    private static JsonObject ParseAuthResponse(HttpStatusCode status, string text, string stage)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject
                ?? throw new TaigaApiException(status, $"Response ({stage}) is not a JSON object: {text}");
        }
        catch (JsonException ex)
        {
            throw new TaigaApiException(status, $"Invalid JSON in response ({stage}): {ex.Message}. Response: {text}");
        }
    }

    /// <summary>Reads the auth_token string field without failing on a wrong value type.</summary>
    private static string ReadAuthToken(JsonObject node, HttpStatusCode status, string stage)
    {
        if (node["auth_token"] is JsonValue value &&
            value.TryGetValue<string>(out var token) &&
            !string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        throw new TaigaApiException(status, $"Response ({stage}) has no valid auth_token field.");
    }

    // ---------------------------------------------------------------------
    // Public operations
    // ---------------------------------------------------------------------

    /// <summary>GET request.</summary>
    public Task<string> GetAsync(string path, IDictionary<string, string?>? query = null, bool? disablePagination = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, path, null, query, disablePagination, ct);

    /// <summary>POST request with a body.</summary>
    public Task<string> PostAsync(string path, JsonNode? body = null, IDictionary<string, string?>? query = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, path, body, query, null, ct);

    /// <summary>PUT request (replaces the whole object).</summary>
    public Task<string> PutAsync(string path, JsonNode? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, path, body, null, null, ct);

    /// <summary>PATCH request (partial update).</summary>
    public Task<string> PatchAsync(string path, JsonNode? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Patch, path, body, null, null, ct);

    /// <summary>DELETE request.</summary>
    public Task<string> DeleteAsync(string path, IDictionary<string, string?>? query = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, path, null, query, null, ct);

    /// <summary>
    /// Main request method. Adds the token automatically
    /// and, on a 401 response, retries once after re-authenticating.
    /// </summary>
    public async Task<string> SendAsync(
        HttpMethod method,
        string path,
        JsonNode? body = null,
        IDictionary<string, string?>? query = null,
        bool? disablePagination = null,
        CancellationToken ct = default)
    {
        var disable = disablePagination ?? _options.DisablePaginationByDefault;

        // Cookie authentication doesn't need a token.
        string? token = IsCookieMode() ? null : await GetTokenAsync(false, ct).ConfigureAwait(false);

        var (status, text, headers) = await SendWithRetryAsync(method, path, body, query, disable, token, ct).ConfigureAwait(false);

        if (status == HttpStatusCode.Unauthorized && !IsCookieMode())
        {
            // The token expired: refresh it and retry once.
            token = await GetTokenAsync(true, ct).ConfigureAwait(false);
            (status, text, headers) = await SendWithRetryAsync(method, path, body, query, disable, token, ct).ConfigureAwait(false);
        }

        if (!IsSuccess(status))
        {
            throw new TaigaApiException(status, string.IsNullOrWhiteSpace(text) ? "(empty response)" : text);
        }

        return FormatResponse(status, text, headers);
    }

    /// <summary>
    /// Sends a request, retrying on transient failures.
    /// Retries on 408/429/5xx statuses and on network exceptions (for idempotent
    /// methods), with exponential backoff and respect for the Retry-After header.
    /// </summary>
    private async Task<(HttpStatusCode Status, string Body, HttpResponseHeaders Headers)> SendWithRetryAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        IDictionary<string, string?>? query,
        bool disablePagination,
        string? token,
        CancellationToken ct)
    {
        var maxRetries = Math.Max(0, _options.MaxRetries);
        var attempt = 0;

        while (true)
        {
            try
            {
                var result = await RawAsync(method, path, body, query, disablePagination, token, ct).ConfigureAwait(false);

                if (attempt < maxRetries && IsTransientStatus(result.Status))
                {
                    await DelayBeforeRetryAsync(attempt, result.Headers, ct).ConfigureAwait(false);
                    attempt++;
                    continue;
                }

                return result;
            }
            catch (Exception ex) when (attempt < maxRetries && IsIdempotent(method) && IsTransientException(ex, ct))
            {
                await DelayBeforeRetryAsync(attempt, null, ct).ConfigureAwait(false);
                attempt++;
            }
        }
    }

    private static bool IsTransientStatus(HttpStatusCode status)
        => status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static bool IsIdempotent(HttpMethod method)
        => method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Put
           || method == HttpMethod.Delete || method == HttpMethod.Options;

    private static bool IsTransientException(Exception ex, CancellationToken ct)
        => ex is HttpRequestException or IOException
           || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private async Task DelayBeforeRetryAsync(int attempt, HttpResponseHeaders? headers, CancellationToken ct)
    {
        // Honor Retry-After if the server sent it.
        var retryAfter = headers?.RetryAfter?.Delta
            ?? (headers?.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        if (retryAfter is { } ra && ra > TimeSpan.Zero && ra <= TimeSpan.FromSeconds(60))
        {
            await Task.Delay(ra, ct).ConfigureAwait(false);
            return;
        }

        var baseMs = Math.Max(0, _options.RetryBaseDelayMs);
        var delayMs = Math.Min(baseMs * Math.Pow(2, attempt), 10_000);
        if (delayMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------------
    // Low-level HTTP
    // ---------------------------------------------------------------------

    private async Task<(HttpStatusCode Status, string Body, HttpResponseHeaders Headers)> RawAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        IDictionary<string, string?>? query,
        bool disablePagination,
        string? token,
        CancellationToken ct)
    {
        var url = AppendQuery(path, query);

        using var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrWhiteSpace(_options.Cookie))
        {
            // SSO session authentication: pass the cookie through as is.
            request.Headers.TryAddWithoutValidation("Cookie", _options.Cookie);
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            // The authentication scheme is configurable: "Bearer" (user token)
            // or "Application" (external Taiga application token).
            var scheme = string.IsNullOrWhiteSpace(_options.AuthScheme) ? "Bearer" : _options.AuthScheme.Trim();
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        }

        if (disablePagination)
        {
            request.Headers.TryAddWithoutValidation("x-disable-pagination", "True");
        }

        if (body is not null)
        {
            var json = body.ToJsonString(WriteOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (response.StatusCode, text, response.Headers);
    }

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and <= 299;

    private static string AppendQuery(string path, IDictionary<string, string?>? query)
    {
        if (query is null || query.Count == 0)
        {
            return path;
        }

        var parts = new List<string>();
        foreach (var kv in query)
        {
            if (string.IsNullOrEmpty(kv.Value))
            {
                continue;
            }

            parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
        }

        if (parts.Count == 0)
        {
            return path;
        }

        var separator = path.Contains('?') ? "&" : "?";
        return path + separator + string.Join('&', parts);
    }

    private static string FormatResponse(HttpStatusCode status, string text, HttpResponseHeaders headers)
    {
        // Empty response (for example 204 No Content): return a small status JSON.
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"{{\"status\": {(int)status}, \"message\": \"{status}\"}}";
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            // The response isn't JSON: return it as is.
            return text;
        }

        if (node is null)
        {
            return $"\"{(int)status} {status}\"";
        }

        return node.ToJsonString(PrettyOptions);
    }
}
