namespace TaigaMcp.Configuration;

/// <summary>
/// Rate limiting settings for the MCP endpoint.
/// Read from the "RateLimit" section and can be overridden by environment variables
/// RATE_LIMIT_ENABLED / RATE_LIMIT_PERMIT / RATE_LIMIT_WINDOW_SECONDS.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>Whether rate limiting is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum requests per window for one key (token or IP).</summary>
    public int PermitLimit { get; set; } = 120;

    /// <summary>Window size in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum length of the queue of requests waiting beyond the limit.
    /// 0 rejects excess requests immediately (429).
    /// </summary>
    public int QueueLimit { get; set; }
}
