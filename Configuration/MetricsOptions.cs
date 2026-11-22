namespace TaigaMcp.Configuration;

/// <summary>
/// Prometheus metrics export settings.
/// Read from the "Metrics" section and can be overridden by environment variables
/// METRICS_ENABLED / METRICS_PATH.
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>Whether metrics collection and export are enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Path that serves the Prometheus metrics.</summary>
    public string Path { get; set; } = "/metrics";
}
