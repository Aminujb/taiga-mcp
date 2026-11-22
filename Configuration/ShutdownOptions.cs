namespace TaigaMcp.Configuration;

/// <summary>
/// Graceful server shutdown settings.
/// Read from the "Shutdown" section and can be overridden by environment variables
/// SHUTDOWN_TIMEOUT_SECONDS / SHUTDOWN_ON_UNHANDLED_EXCEPTION.
/// </summary>
public sealed class ShutdownOptions
{
    /// <summary>
    /// How many seconds to wait for in-flight requests when stopping,
    /// before ending them forcibly.
    /// </summary>
    public int ShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Start a graceful shutdown on an unhandled exception.
    /// </summary>
    public bool ShutdownOnUnhandledException { get; set; } = true;
}
