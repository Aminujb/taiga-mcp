using System.Text.Json;
using System.Text.Json.Nodes;
using TaigaMcp.Configuration;

namespace TaigaMcp.Logging;

/// <summary>
/// Audit log of destructive MCP tool calls.
///
/// Each call is recorded as a structured entry in <see cref="ILogger"/>
/// (category "TaigaMcp.Audit") and, if <see cref="AuditOptions.FilePath"/> is set,
/// also appended to the file as a JSON line (JSON Lines format).
/// </summary>
public sealed class AuditLogger
{
    // Audit file: compact single-line entries; non-ASCII text is not escaped.
    private static readonly JsonSerializerOptions FileJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly ILogger<AuditLogger> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditOptions _options;
    private readonly object _fileLock = new();

    public AuditLogger(ILogger<AuditLogger> logger, IHttpContextAccessor httpContextAccessor, AuditOptions options)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    /// <summary>
    /// Records the result of a destructive tool call.
    /// </summary>
    /// <param name="tool">MCP tool name (for example taiga_delete_project).</param>
    /// <param name="arguments">Call arguments (an anonymous object or dictionary).</param>
    /// <param name="success">Whether the call succeeded.</param>
    /// <param name="error">Error text on failure.</param>
    public void Record(string tool, object? arguments, bool success, string? error)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var clientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var argsNode = SerializeArguments(arguments);

        _logger.LogInformation(
            "AUDIT tool={Tool} success={Success} clientIp={ClientIp} error={Error} arguments={Arguments}",
            tool,
            success,
            clientIp ?? "unknown",
            error ?? "-",
            argsNode?.ToJsonString() ?? "{}");

        WriteEntry(new AuditEntry(DateTimeOffset.UtcNow, tool, success, clientIp, error, argsNode));
    }

    private static JsonNode? SerializeArguments(object? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        if (arguments is JsonNode node)
        {
            return node.DeepClone();
        }

        return JsonSerializer.SerializeToNode(arguments);
    }

    private void WriteEntry(AuditEntry entry)
    {
        if (string.IsNullOrWhiteSpace(_options.FilePath))
        {
            return;
        }

        try
        {
            var path = ResolvePath(_options.FilePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(entry, FileJsonOptions);
            lock (_fileLock)
            {
                RotateIfNeeded(path);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // A failure to write the audit log must not break the tool call.
            _logger.LogWarning(ex, "Failed to write the audit log to file {FilePath}", _options.FilePath);
        }
    }

    /// <summary>
    /// Rotates the audit file when it reaches <see cref="AuditOptions.MaxFileSizeMB"/>:
    /// shifts the archived copies (.1 … .N), deletes the oldest and creates a new file.
    /// Called while holding <c>_fileLock</c>.
    /// </summary>
    private void RotateIfNeeded(string path)
    {
        if (_options.MaxFileSizeMB <= 0 || !File.Exists(path))
        {
            return;
        }

        var maxBytes = (long)_options.MaxFileSizeMB * 1024 * 1024;
        if (new FileInfo(path).Length < maxBytes)
        {
            return;
        }

        var maxBackups = Math.Max(1, _options.MaxBackupFiles);

        var oldest = $"{path}.{maxBackups}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = _options.MaxBackupFiles - 1; index >= 1; index--)
        {
            var source = $"{path}.{index}";
            if (File.Exists(source))
            {
                File.Move(source, $"{path}.{index + 1}", overwrite: true);
            }
        }

        File.Move(path, path + ".1", overwrite: true);
        _logger.LogInformation(
            "Audit log {FilePath} reached {MaxSizeMB} MB and was rotated ({Backups} archived copies).",
            path,
            _options.MaxFileSizeMB,
            _options.MaxBackupFiles);
    }

    private static string ResolvePath(string path)
        => Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    private sealed record AuditEntry(
        DateTimeOffset Timestamp,
        string Tool,
        bool Success,
        string? ClientIp,
        string? Error,
        JsonNode? Arguments);
}
