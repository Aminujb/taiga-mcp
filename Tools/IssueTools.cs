using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Tools for managing Taiga issues.</summary>
[McpServerToolType]
public sealed class IssueTools : TaigaToolBase
{
    public IssueTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_issues", ReadOnly = true)]
    [Description("Lists issues, with filters.")]
    public Task<string> ListIssuesAsync(
        [Description("Project ID.")] int? project = null,
        [Description("Status ID.")] int? status = null,
        [Description("Severity ID.")] int? severity = null,
        [Description("Priority ID.")] int? priority = null,
        [Description("Issue type ID.")] int? type = null,
        [Description("Owner ID.")] int? owner = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Role ID.")] int? role = null,
        [Description("Watcher ID.")] int? watchers = null,
        [Description("Whether the status is closed (true/false).")] bool? status__is_closed = null,
        [Description("Comma-separated tags.")] string? tags = null,
        [Description("Sort field: type, severity, status, priority, created_date, modified_date, owner, assigned_to, subject.")] string? order_by = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["status"] = Q(status),
                ["severity"] = Q(severity),
                ["priority"] = Q(priority),
                ["type"] = Q(type),
                ["owner"] = Q(owner),
                ["assigned_to"] = Q(assigned_to),
                ["role"] = Q(role),
                ["watchers"] = Q(watchers),
                ["status__is_closed"] = Q(status__is_closed),
                ["tags"] = tags,
                ["order_by"] = order_by,
            };
            return await Client.GetAsync("issues", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_issue", ReadOnly = true)]
    [Description("Gets an issue by ID.")]
    public Task<string> GetIssueAsync(
        [Description("Issue ID.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"issues/{issue_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_issue_by_ref", ReadOnly = true)]
    [Description("Gets an issue by its number (ref) within a project.")]
    public Task<string> GetIssueByRefAsync(
        [Description("Issue number (ref).")] int @ref,
        [Description("Project ID.")] int? project = null,
        [Description("Project slug.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("issues/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_issue")]
    [Description("Creates an issue. project and subject are required.")]
    public Task<string> CreateIssueAsync(
        [Description("Project ID (required).")] int project,
        [Description("Issue subject (required).")] string subject,
        [Description("Description.")] string? description = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Status ID.")] int? status = null,
        [Description("Severity ID.")] int? severity = null,
        [Description("Priority ID.")] int? priority = null,
        [Description("Issue type ID.")] int? type = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("The issue is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The issue is closed.")] bool? is_closed = null,
        [Description("Tags.")] string[]? tags = null,
        [Description("Watcher IDs.")] int[]? watchers = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("subject", subject),
                ("description", description),
                ("assigned_to", assigned_to),
                ("status", status),
                ("severity", severity),
                ("priority", priority),
                ("type", type),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("tags", tags),
                ("watchers", watchers));
            return Client.PostAsync("issues", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_issue")]
    [Description("Partially updates a issue (PATCH): only the fields given change. Pass version for optimistic concurrency. comment adds a comment to the ticket's history.")]
    public Task<string> UpdateIssueAsync(
        [Description("Issue ID.")] int issue_id,
        [Description("Current object version for OCC (recommended).")] int? version = null,
        [Description("Comment added to the ticket's history with this change.")] string? comment = null,
        [Description("Subject.")] string? subject = null,
        [Description("Description.")] string? description = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Status ID.")] int? status = null,
        [Description("Severity ID.")] int? severity = null,
        [Description("Priority ID.")] int? priority = null,
        [Description("Issue type ID.")] int? type = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("The issue is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The issue is closed.")] bool? is_closed = null,
        [Description("Tags.")] string[]? tags = null,
        [Description("Watcher IDs.")] int[]? watchers = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("comment", comment),
                ("subject", subject),
                ("description", description),
                ("assigned_to", assigned_to),
                ("status", status),
                ("severity", severity),
                ("priority", priority),
                ("type", type),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"issues/{issue_id}";
            return Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_issue", Destructive = true)]
    [Description("Deletes an issue by ID.")]
    public Task<string> DeleteIssueAsync(
        [Description("Issue ID.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_issue",
            new { issue_id },
            () => Client.DeleteAsync($"issues/{issue_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_watch_issue")]
    [Description("Subscribes to notifications for an issue.")]
    public Task<string> WatchIssueAsync(
        [Description("Issue ID.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"issues/{issue_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_issue")]
    [Description("Unsubscribes from notifications for an issue.")]
    public Task<string> UnwatchIssueAsync(
        [Description("Issue ID.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"issues/{issue_id}/unwatch", ct: cancellationToken));
    }
}
