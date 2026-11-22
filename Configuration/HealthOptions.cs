namespace TaigaMcp.Configuration;

/// <summary>
/// Health check settings (the "Health" section).
/// Overridden by the HEALTH_READY_CACHE_SECONDS environment variable.
/// </summary>
public sealed class HealthOptions
{
    /// <summary>
    /// How long the readiness check result is cached, in seconds.
    /// The real request to Taiga runs at most once per interval,
    /// and frequent polls (for example every 5 seconds) get the saved result.
    /// 0 checks availability on every request.
    /// </summary>
    public int ReadyCacheSeconds { get; set; } = 60;
}
