namespace TaigaMcp.Configuration;

/// <summary>
/// Settings for the audit log of destructive operations.
/// Read from the "Audit" section and can be overridden by environment variables
/// AUDIT_ENABLED / AUDIT_FILE_PATH.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>Whether the audit log is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path to the audit log file (JSON Lines format).
    /// If empty, entries go to ILogger only (category "TaigaMcp.Audit").
    /// A relative path is resolved against the build output directory.
    /// </summary>
    public string? FilePath { get; set; } = "logs/audit.log";

    /// <summary>
    /// Maximum audit file size in megabytes before rotation. 0 disables rotation.
    /// Defaults to 64 MB.
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 64;

    /// <summary>
    /// Number of archived copies kept on rotation (audit.log.1 … audit.log.N).
    /// The oldest file is deleted.
    /// </summary>
    public int MaxBackupFiles { get; set; } = 5;
}
