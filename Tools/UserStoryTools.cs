using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Tools for managing Taiga user stories.</summary>
[McpServerToolType]
public sealed class UserStoryTools : TaigaToolBase
{
    public UserStoryTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_userstories", ReadOnly = true)]
    [Description("Lists user stories, with filters.")]
    public Task<string> ListUserStoriesAsync(
        [Description("Project ID.")] int? project = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("Whether the story has no milestone (true/false).")] bool? milestone__isnull = null,
        [Description("Status ID.")] int? status = null,
        [Description("Whether the status is closed (true/false).")] bool? status__is_closed = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Epic ID.")] int? epic = null,
        [Description("Role ID.")] int? role = null,
        [Description("Watcher ID.")] int? watchers = null,
        [Description("Comma-separated tags.")] string? tags = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["milestone"] = Q(milestone),
                ["milestone__isnull"] = Q(milestone__isnull),
                ["status"] = Q(status),
                ["status__is_closed"] = Q(status__is_closed),
                ["assigned_to"] = Q(assigned_to),
                ["epic"] = Q(epic),
                ["role"] = Q(role),
                ["watchers"] = Q(watchers),
                ["tags"] = tags,
            };
            return await Client.GetAsync("userstories", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_userstory", ReadOnly = true)]
    [Description("Gets a user story by ID.")]
    public Task<string> GetUserStoryAsync(
        [Description("Story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"userstories/{user_story_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_userstory_by_ref", ReadOnly = true)]
    [Description("Gets a user story by its number (ref) within a project.")]
    public Task<string> GetUserStoryByRefAsync(
        [Description("Story number (ref).")] int @ref,
        [Description("Project ID.")] int? project = null,
        [Description("Project slug.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("userstories/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_userstory")]
    [Description("Creates a user story. project and subject are required.")]
    public Task<string> CreateUserStoryAsync(
        [Description("Project ID (required).")] int project,
        [Description("Story subject (required).")] string subject,
        [Description("Description.")] string? description = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Status ID.")] int? status = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("The story is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The story is closed.")] bool? is_closed = null,
        [Description("Client requirement.")] bool? client_requirement = null,
        [Description("Team requirement.")] bool? team_requirement = null,
        [Description("Points: a dictionary of the form {\"role_ID\": value}.")] Dictionary<string, double>? points = null,
        [Description("Tags.")] string[]? tags = null,
        [Description("Watcher IDs.")] int[]? watchers = null,
        [Description("Backlog order.")] int? backlog_order = null,
        [Description("Sprint order.")] int? sprint_order = null,
        [Description("Kanban order.")] int? kanban_order = null,
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
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("points", points),
                ("tags", tags),
                ("watchers", watchers),
                ("backlog_order", backlog_order),
                ("sprint_order", sprint_order),
                ("kanban_order", kanban_order));
            return Client.PostAsync("userstories", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_userstory")]
    [Description("Partially updates a user story (PATCH): only the fields given change. Pass version for optimistic concurrency. comment adds a comment to the ticket's history.")]
    public Task<string> UpdateUserStoryAsync(
        [Description("Story ID.")] int user_story_id,
        [Description("Current object version for OCC (recommended).")] int? version = null,
        [Description("Comment added to the ticket's history with this change.")] string? comment = null,
        [Description("Subject.")] string? subject = null,
        [Description("Description.")] string? description = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Status ID.")] int? status = null,
        [Description("Milestone (sprint) ID.")] int? milestone = null,
        [Description("The story is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The story is closed.")] bool? is_closed = null,
        [Description("Client requirement.")] bool? client_requirement = null,
        [Description("Team requirement.")] bool? team_requirement = null,
        [Description("Points: a dictionary {\"role_ID\": value}.")] Dictionary<string, double>? points = null,
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
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("points", points),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"userstories/{user_story_id}";
            return Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_userstory", Destructive = true)]
    [Description("Deletes a user story by ID.")]
    public Task<string> DeleteUserStoryAsync(
        [Description("Story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_userstory",
            new { user_story_id },
            () => Client.DeleteAsync($"userstories/{user_story_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_bulk_create_userstories")]
    [Description("Creates stories in bulk. Each item in subjects becomes one story.")]
    public Task<string> BulkCreateUserStoriesAsync(
        [Description("Project ID (required).")] int project_id,
        [Description("List of story subjects (required).")] string[] subjects,
        [Description("Status ID.")] int? status_id = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("status_id", status_id),
                ("bulk_stories", string.Join('\n', subjects)));
            return Client.PostAsync("userstories/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_watch_userstory")]
    [Description("Subscribes to notifications for a story.")]
    public Task<string> WatchUserStoryAsync(
        [Description("Story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_userstory")]
    [Description("Unsubscribes from notifications for a story.")]
    public Task<string> UnwatchUserStoryAsync(
        [Description("Story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/unwatch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_vote_userstory")]
    [Description("Votes for a story (upvote).")]
    public Task<string> VoteUserStoryAsync(
        [Description("Story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/upvote", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unvote_userstory")]
    [Description("Removes a vote from a story (downvote).")]
    public Task<string> UnvoteUserStoryAsync(
        [Description("Story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/downvote", ct: cancellationToken));
    }
}
