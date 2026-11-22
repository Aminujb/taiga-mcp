using Prometheus;

namespace TaigaMcp.Metrics;

/// <summary>
/// MCP tool call metrics in Prometheus format:
///  - taiga_mcp_tool_requests_total{tool,status}          - RPS (number of calls);
///  - taiga_mcp_tool_errors_total{tool}                   - error rate (number of errors);
///  - taiga_mcp_tool_request_duration_seconds{tool}       - latency (duration histogram).
/// </summary>
public sealed class McpMetrics
{
    public const string RequestsMetricName = "taiga_mcp_tool_requests_total";
    public const string ErrorsMetricName = "taiga_mcp_tool_errors_total";
    public const string DurationMetricName = "taiga_mcp_tool_request_duration_seconds";

    public const string SuccessStatus = "success";
    public const string ErrorStatus = "error";

    private readonly Counter _requests;
    private readonly Counter _errors;
    private readonly Histogram _duration;

    /// <summary>Creates the metrics in the global Prometheus registry.</summary>
    public McpMetrics()
        : this(Prometheus.Metrics.DefaultRegistry)
    {
    }

    /// <summary>Creates the metrics in the given registry (used in tests).</summary>
    public McpMetrics(CollectorRegistry registry)
    {
        var factory = Prometheus.Metrics.WithCustomRegistry(registry);

        _requests = factory.CreateCounter(
            RequestsMetricName,
            "Total number of MCP tool calls.",
            new[] { "tool", "status" });

        _errors = factory.CreateCounter(
            ErrorsMetricName,
            "Number of MCP tool calls that ended in an error.",
            new[] { "tool" });

        _duration = factory.CreateHistogram(
            DurationMetricName,
            "Duration of an MCP tool call in seconds.",
            new[] { "tool" });
    }

    /// <summary>Starts measuring the duration of a tool call.</summary>
    public IDisposable MeasureDuration(string tool)
        => _duration.WithLabels(Normalize(tool)).NewTimer();

    /// <summary>Records the result of a tool call (success or error).</summary>
    public void Observe(string tool, bool success)
    {
        var name = Normalize(tool);
        _requests.WithLabels(name, success ? SuccessStatus : ErrorStatus).Inc();
        if (!success)
        {
            _errors.WithLabels(name).Inc();
        }
    }

    private static string Normalize(string? tool)
        => string.IsNullOrWhiteSpace(tool) ? "unknown" : tool;
}
