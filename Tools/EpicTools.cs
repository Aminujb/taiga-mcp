using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Tools for managing Taiga epics.</summary>
[McpServerToolType]
public sealed class EpicTools : TaigaToolBase
{
    public EpicTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_epics", ReadOnly = true)]
    [Description("Lists epics, with filters.")]
    public Task<string> ListEpicsAsync(
        [Description("Project ID.")] int? project = null,
        [Description("Project slug.")] string? project__slug = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Whether the epic status is closed (true/false).")] bool? status__is_closed = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
                ["assigned_to"] = Q(assigned_to),
                ["status__is_closed"] = Q(status__is_closed),
            };
            return await Client.GetAsync("epics", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_epic", ReadOnly = true)]
    [Description("Gets an epic by ID.")]
    public Task<string> GetEpicAsync(
        [Description("Epic ID.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"epics/{epic_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_epic_by_ref", ReadOnly = true)]
    [Description("Gets an epic by its number (ref) within a project.")]
    public Task<string> GetEpicByRefAsync(
        [Description("Epic number (ref).")] int @ref,
        [Description("Project ID.")] int? project = null,
        [Description("Project slug.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("epics/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_epic")]
    [Description("Creates an epic. project and subject are required.")]
    public Task<string> CreateEpicAsync(
        [Description("Project ID (required).")] int project,
        [Description("Epic subject (required).")] string subject,
        [Description("Description.")] string? description = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Epic status ID.")] int? status = null,
        [Description("Color in HEX format, for example #ABCABC.")] string? color = null,
        [Description("The epic is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The epic is closed.")] bool? is_closed = null,
        [Description("Client requirement.")] bool? client_requirement = null,
        [Description("Team requirement.")] bool? team_requirement = null,
        [Description("Epic order (epics_order).")] int? epics_order = null,
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
                ("color", color),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("epics_order", epics_order),
                ("tags", tags),
                ("watchers", watchers));
            return Client.PostAsync("epics", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_epic")]
    [Description("Updates an epic. PATCH by default; replace_all=true uses PUT. Pass version for OCC.")]
    public Task<string> UpdateEpicAsync(
        [Description("Epic ID.")] int epic_id,
        [Description("Current object version for OCC (recommended).")] int? version = null,
        [Description("Replace the whole object (PUT) instead of a partial update (PATCH).")] bool replace_all = false,
        [Description("Subject.")] string? subject = null,
        [Description("Description.")] string? description = null,
        [Description("ID of the assigned user.")] int? assigned_to = null,
        [Description("Epic status ID.")] int? status = null,
        [Description("Color in HEX format.")] string? color = null,
        [Description("The epic is blocked.")] bool? is_blocked = null,
        [Description("Reason for the block.")] string? blocked_note = null,
        [Description("The epic is closed.")] bool? is_closed = null,
        [Description("Client requirement.")] bool? client_requirement = null,
        [Description("Team requirement.")] bool? team_requirement = null,
        [Description("Tags.")] string[]? tags = null,
        [Description("Watcher IDs.")] int[]? watchers = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("subject", subject),
                ("description", description),
                ("assigned_to", assigned_to),
                ("status", status),
                ("color", color),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"epics/{epic_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_epic", Destructive = true)]
    [Description("Deletes an epic by ID.")]
    public Task<string> DeleteEpicAsync(
        [Description("Epic ID.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_epic",
            new { epic_id },
            () => Client.DeleteAsync($"epics/{epic_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_bulk_create_epics")]
    [Description("Creates epics in bulk. Each item in subjects becomes one epic.")]
    public Task<string> BulkCreateEpicsAsync(
        [Description("Project ID (required).")] int project_id,
        [Description("List of epic subjects (required).")] string[] subjects,
        [Description("Epic status ID.")] int? status_id = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("status_id", status_id),
                ("bulk_epics", string.Join('\n', subjects)));
            return Client.PostAsync("epics/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_list_epic_related_userstories", ReadOnly = true)]
    [Description("Lists the user stories related to an epic.")]
    public Task<string> ListEpicRelatedUserStoriesAsync(
        [Description("Epic ID.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"epics/{epic_id}/related_userstories", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_epic_related_userstory")]
    [Description("Links a user story to an epic.")]
    public Task<string> CreateEpicRelatedUserStoryAsync(
        [Description("Epic ID.")] int epic_id,
        [Description("User story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("epic", epic_id),
                ("user_story", user_story_id));
            return Client.PostAsync($"epics/{epic_id}/related_userstories", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_epic_related_userstory", Destructive = true)]
    [Description("Removes the link between a user story and an epic.")]
    public Task<string> DeleteEpicRelatedUserStoryAsync(
        [Description("Epic ID.")] int epic_id,
        [Description("User story ID.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_epic_related_userstory",
            new { epic_id, user_story_id },
            () => Client.DeleteAsync($"epics/{epic_id}/related_userstories/{user_story_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_vote_epic")]
    [Description("Votes for an epic (upvote).")]
    public Task<string> VoteEpicAsync(
        [Description("Epic ID.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"epics/{epic_id}/upvote", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_watch_epic")]
    [Description("Subscribes to notifications for an epic.")]
    public Task<string> WatchEpicAsync(
        [Description("Epic ID.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"epics/{epic_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_epic")]
    [Description("Unsubscribes from notifications for an epic.")]
    public Task<string> UnwatchEpicAsync(
        [Description("Epic ID.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"epics/{epic_id}/unwatch", ct: cancellationToken));
    }
}
