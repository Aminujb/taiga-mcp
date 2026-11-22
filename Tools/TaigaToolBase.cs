using System.Globalization;
using System.Runtime.CompilerServices;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>
/// Base class for MCP tools.
///
/// Holds the Taiga client, audit log and metrics, and provides execution wrappers
/// that turn exceptions into a clear text message for the client,
/// record metrics and (for destructive tools) write an audit log entry.
/// </summary>
public abstract class TaigaToolBase
{
    protected TaigaToolBase(TaigaClient client, AuditLogger audit, McpMetrics metrics)
    {
        Client = client;
        Audit = audit;
        Metrics = metrics;
    }

    /// <summary>Taiga REST API client.</summary>
    protected TaigaClient Client { get; }

    /// <summary>Audit log of destructive operations.</summary>
    protected AuditLogger Audit { get; }

    /// <summary>Tool call metrics (Prometheus).</summary>
    protected McpMetrics Metrics { get; }

    /// <summary>
    /// Runs a tool action and turns exceptions into a clear text message,
    /// so the MCP client gets an error description rather than a server crash.
    /// The tool name for metrics is resolved from the calling method's name.
    /// </summary>
    protected async Task<string> RunAsync(Func<Task<string>> action, [CallerMemberName] string memberName = "")
    {
        var tool = ToolNameResolver.Resolve(GetType(), memberName);
        using var timer = Metrics.MeasureDuration(tool);
        try
        {
            var result = await action().ConfigureAwait(false);
            Metrics.Observe(tool, success: true);
            return result;
        }
        catch (Exception ex)
        {
            Metrics.Observe(tool, success: false);
            return FormatError(ex);
        }
    }

    /// <summary>
    /// Runs a destructive tool: writes an audit log entry
    /// (tool name, arguments, result, client IP, time) and records metrics.
    /// </summary>
    protected async Task<string> RunDestructiveAsync(
        string toolName,
        object? arguments,
        Func<Task<string>> action)
    {
        using var timer = Metrics.MeasureDuration(toolName);
        try
        {
            var result = await action().ConfigureAwait(false);
            Metrics.Observe(toolName, success: true);
            Audit.Record(toolName, arguments, success: true, error: null);
            return result;
        }
        catch (Exception ex)
        {
            Metrics.Observe(toolName, success: false);
            Audit.Record(toolName, arguments, success: false, error: ex.Message);
            return FormatError(ex);
        }
    }

    private static string FormatError(Exception ex)
    {
        if (ex is TaigaApiException api)
        {
            return $"Taiga API error ({(int)api.StatusCode} {api.StatusCode}):\n{api.ResponseBody}";
        }
        return "Tool execution error: " + ex.Message;
    }

    protected static string? Q(int? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    protected static string? Q(bool? value)
        => value.HasValue ? (value.Value ? "true" : "false") : null;
}
