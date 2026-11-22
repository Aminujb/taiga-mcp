namespace TaigaMcp.Configuration;

/// <summary>
/// Taiga REST API connection settings.
/// Values come from the "Taiga" configuration section (appsettings.json)
/// and can be overridden by environment variables (TAIGA_*).
/// </summary>
public sealed class TaigaOptions
{
    /// <summary>
    /// Base address of the Taiga API. Defaults to the Taiga cloud server.
    /// Example: https://api.taiga.io/api/v1 or http://localhost:8000/api/v1
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.taiga.io/api/v1";

    /// <summary>Login (username or email) for standard authentication.</summary>
    public string? Username { get; set; }

    /// <summary>Password for standard authentication.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// A ready-made access token (auth_token). If set, no username/password login is performed.
    /// Takes priority over Username/Password.
    ///
    /// This is an alternative for cases where direct API login isn't available
    /// (for example because of SSO): the token can be taken from an open Taiga session
    /// (the "taiga.auth_token" localStorage key or the Authorization header) and passed here.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>Refresh token (optional).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Authentication method:
    ///  - "auto" (default): the configured Token, then RefreshToken, then Username/Password login;
    ///  - "password": always log in with Username/Password (even if Token is set);
    ///  - "token": use only the configured Token, with no API login
    ///             (for SSO: the token comes from an open Taiga session);
    ///  - "cookie": send the Cookie header (SSO/proxy session); the Authorization header is not used.
    /// </summary>
    public string AuthMethod { get; set; } = "auto";

    /// <summary>
    /// Value of the Cookie header for AuthMethod="cookie".
    /// Used when the API can only be reached with an SSO session cookie
    /// (for example Taiga behind a reverse proxy with SSO authentication).
    /// Example: "sessionid=abc123; csrftoken=xyz".
    /// </summary>
    public string? Cookie { get; set; }

    /// <summary>
    /// Scheme used in the Authorization header.
    /// "Bearer" is the standard Taiga user token (default).
    /// "Application" is an external application token (Authorization: Application {token}).
    /// </summary>
    public string AuthScheme { get; set; } = "Bearer";

    /// <summary>
    /// Path to a certificate (PEM/DER/PFX) to trust.
    /// A root CA is validated through the chain; a leaf or self-signed
    /// certificate is pinned by thumbprint. Preferred over IgnoreSslErrors.
    /// A relative path is resolved against the build output directory.
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Ignore TLS certificate validation errors for the Taiga server.
    /// Needed for corporate or self-signed certificates the OS doesn't trust.
    /// Enable only for trusted internal installations.
    /// </summary>
    public bool IgnoreSslErrors { get; set; }

    /// <summary>
    /// Disable pagination (the x-disable-pagination header) for list requests by default.
    /// true returns all records at once.
    /// </summary>
    public bool DisablePaginationByDefault { get; set; } = true;

    /// <summary>Timeout for HTTP requests to Taiga, in seconds. Defaults to 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retries on transient failures (network, 429/5xx).
    /// 0 disables retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay before a retry (ms); grows exponentially.</summary>
    public int RetryBaseDelayMs { get; set; } = 500;
}
