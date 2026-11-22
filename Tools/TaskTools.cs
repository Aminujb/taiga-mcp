using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Tools for managing Taiga tasks.</summary>
[McpServerToolType]
public sealed class TaskTools : TaigaToolBase
{
    public TaskTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_tasks", ReadOnly = true)]
    [Description("Lists tasks, with filters.")]
    public Task<string> ListTasksAsync(
        [Description("Project ID.")] int? project = null,
        [Description("Status ID.")] int? status = null,
        [Description("User story ID.")] int? user_story = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("Owner ID.")] int? owner = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Role ID.")] int? role = null,
        [Description("Watcher ID.")] int? watchers = null,
        [Description("Whether the status is closed (true/false).")] bool? status__is_closed = null,
        [Description("Comma-separated tags.")] string? tags = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["status"] = Q(status),
                ["user_story"] = Q(user_story),
                ["milestone"] = Q(milestone),
                ["owner"] = Q(owner),
                ["assigned_to"] = Q(assigned_to),
                ["role"] = Q(role),
                ["watchers"] = Q(watchers),
                ["status__is_closed"] = Q(status__is_closed),
                ["tags"] = tags,
            };
            return await Client.GetAsync("tasks", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_task", ReadOnly = true)]
    [Description("Gets a task by ID.")]
    public Task<string> GetTaskAsync(
        [Description("Task ID.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"tasks/{task_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_task_by_ref", ReadOnly = true)]
    [Description("Gets a task by its number (ref) within a project.")]
    public Task<string> GetTaskByRefAsync(
        [Description("Task number (ref).")] int @ref,
        [Description("Project ID.")] int? project = null,
        [Description("Project slug.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("tasks/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_task")]
    [Description("Creates a task. project and subject are required.")]
    public Task<string> CreateTaskAsync(
        [Description("Project ID (required).")] int project,
        [Description("Task subject (required).")] string subject,
        [Description("Description.")] string? description = null,
        [Description("User story ID.")] int? user_story = null,
        [Description("Status ID.")] int? status = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("The task is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The task is closed.")] bool? is_closed = null,
        [Description("The iocaine flag.")] bool? is_iocaine = null,
        [Description("Tags.")] string[]? tags = null,
        [Description("Watcher IDs.")] int[]? watchers = null,
        [Description("Order within the user story (us_order).")] int? us_order = null,
        [Description("Order on the taskboard.")] int? taskboard_order = null,
        [Description("External reference [\"service\", \"serviceId\"].")] string[]? external_reference = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("subject", subject),
                ("description", description),
                ("user_story", user_story),
                ("status", status),
                ("assigned_to", assigned_to),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("is_iocaine", is_iocaine),
                ("tags", tags),
                ("watchers", watchers),
                ("us_order", us_order),
                ("taskboard_order", taskboard_order),
                ("external_reference", external_reference));
            return Client.PostAsync("tasks", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_task")]
    [Description("Partially updates a task (PATCH): only the fields given change. Pass version for optimistic concurrency. comment adds a comment to the ticket's history.")]
    public Task<string> UpdateTaskAsync(
        [Description("Task ID.")] int task_id,
        [Description("Current object version for OCC (recommended).")] int? version = null,
        [Description("Comment added to the ticket's history with this change.")] string? comment = null,
        [Description("Subject.")] string? subject = null,
        [Description("Description.")] string? description = null,
        [Description("User story ID.")] int? user_story = null,
        [Description("Status ID.")] int? status = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("The task is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The task is closed.")] bool? is_closed = null,
        [Description("The iocaine flag.")] bool? is_iocaine = null,
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
                ("user_story", user_story),
                ("status", status),
                ("assigned_to", assigned_to),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("is_iocaine", is_iocaine),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"tasks/{task_id}";
            return Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_task", Destructive = true)]
    [Description("Deletes a task by ID.")]
    public Task<string> DeleteTaskAsync(
        [Description("Task ID.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_task",
            new { task_id },
            () => Client.DeleteAsync($"tasks/{task_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_bulk_create_tasks")]
    [Description("Creates tasks in bulk. Each item in subjects becomes one task.")]
    public Task<string> BulkCreateTasksAsync(
        [Description("Project ID (required).")] int project_id,
        [Description("List of task subjects (required).")] string[] subjects,
        [Description("Status ID.")] int? status_id = null,
        [Description("Milestone (sprint) ID.")] int? milestone_id = null,
        [Description("User story ID.")] int? us_id = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("status_id", status_id),
                ("milestone_id", milestone_id),
                ("us_id", us_id),
                ("bulk_tasks", string.Join('\n', subjects)));
            return Client.PostAsync("tasks/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_watch_task")]
    [Description("Subscribes to notifications for a task.")]
    public Task<string> WatchTaskAsync(
        [Description("Task ID.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"tasks/{task_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_task")]
    [Description("Unsubscribes from notifications for a task.")]
    public Task<string> UnwatchTaskAsync(
        [Description("Task ID.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"tasks/{task_id}/unwatch", ct: cancellationToken));
    }
}
