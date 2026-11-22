using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Tools for managing Taiga projects.</summary>
[McpServerToolType]
public sealed class ProjectTools : TaigaToolBase
{
    public ProjectTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_projects", ReadOnly = true)]
    [Description("Lists Taiga projects. Supports filters and sorting.")]
    public Task<string> ListProjectsAsync(
        [Description("Member ID, to filter to projects that member belongs to.")] int? member = null,
        [Description("Comma-separated member IDs.")] string? members = null,
        [Description("The project is looking for new members (true/false).")] bool? is_looking_for_people = null,
        [Description("The project is marked as featured (true/false).")] bool? is_featured = null,
        [Description("The backlog is activated (true/false).")] bool? is_backlog_activated = null,
        [Description("Kanban is activated (true/false).")] bool? is_kanban_activated = null,
        [Description("Sort field: memberships__user_order, total_fans, total_activity, total_fans_last_week, etc.")] string? order_by = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        [Description("Page number (when pagination is enabled).")] int? page = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["member"] = Q(member),
                ["members"] = members,
                ["is_looking_for_people"] = Q(is_looking_for_people),
                ["is_featured"] = Q(is_featured),
                ["is_backlog_activated"] = Q(is_backlog_activated),
                ["is_kanban_activated"] = Q(is_kanban_activated),
                ["order_by"] = order_by,
                ["page"] = Q(page),
            };
            return await Client.GetAsync("projects", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_project", ReadOnly = true)]
    [Description("Gets a project by its ID.")]
    public Task<string> GetProjectAsync(
        [Description("Project ID.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"projects/{project_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_project_by_slug", ReadOnly = true)]
    [Description("Gets a project by its slug.")]
    public Task<string> GetProjectBySlugAsync(
        [Description("Project slug, for example 'project-0'.")] string slug,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("projects/by_slug",
            new Dictionary<string, string?> { ["slug"] = slug },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_project")]
    [Description("Creates a new Taiga project. name and description are required.")]
    public Task<string> CreateProjectAsync(
        [Description("Project name (required).")] string name,
        [Description("Project description (required).")] string description,
        [Description("ID of the base project template (creation_template).")] int? creation_template = null,
        [Description("Activate the backlog.")] bool? is_backlog_activated = null,
        [Description("Activate the Issues module.")] bool? is_issues_activated = null,
        [Description("Activate kanban.")] bool? is_kanban_activated = null,
        [Description("Make the project private.")] bool? is_private = null,
        [Description("Activate the wiki.")] bool? is_wiki_activated = null,
        [Description("Video conference service: whereby-com, jitsi, talky or custom.")] string? videoconferences = null,
        [Description("Extra data for generating the video conference link.")] string? videoconferences_extra_data = null,
        [Description("Total number of milestones.")] int? total_milestones = null,
        [Description("Total number of story points.")] double? total_story_points = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("name", name),
                ("description", description),
                ("creation_template", creation_template),
                ("is_backlog_activated", is_backlog_activated),
                ("is_issues_activated", is_issues_activated),
                ("is_kanban_activated", is_kanban_activated),
                ("is_private", is_private),
                ("is_wiki_activated", is_wiki_activated),
                ("videoconferences", videoconferences),
                ("videoconferences_extra_data", videoconferences_extra_data),
                ("total_milestones", total_milestones),
                ("total_story_points", total_story_points));
            return Client.PostAsync("projects", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_project")]
    [Description("Updates a project. Partial update (PATCH) by default; replace_all=true replaces it entirely (PUT).")]
    public Task<string> UpdateProjectAsync(
        [Description("Project ID.")] int project_id,
        [Description("Current object version for OCC (optional but recommended).")] int? version = null,
        [Description("Replace the whole object (PUT) instead of a partial update (PATCH).")] bool replace_all = false,
        [Description("New project name.")] string? name = null,
        [Description("New project description.")] string? description = null,
        [Description("Activate the backlog.")] bool? is_backlog_activated = null,
        [Description("Activate the Issues module.")] bool? is_issues_activated = null,
        [Description("Activate kanban.")] bool? is_kanban_activated = null,
        [Description("Make the project private.")] bool? is_private = null,
        [Description("Activate the wiki.")] bool? is_wiki_activated = null,
        [Description("Video conference service: whereby-com, jitsi, talky or custom.")] string? videoconferences = null,
        [Description("Extra data for the video conference.")] string? videoconferences_extra_data = null,
        [Description("The project is looking for new members.")] bool? is_looking_for_people = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("name", name),
                ("description", description),
                ("is_backlog_activated", is_backlog_activated),
                ("is_issues_activated", is_issues_activated),
                ("is_kanban_activated", is_kanban_activated),
                ("is_private", is_private),
                ("is_wiki_activated", is_wiki_activated),
                ("videoconferences", videoconferences),
                ("videoconferences_extra_data", videoconferences_extra_data),
                ("is_looking_for_people", is_looking_for_people));
            var path = $"projects/{project_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_project", Destructive = true)]
    [Description("Deletes a project by ID.")]
    public Task<string> DeleteProjectAsync(
        [Description("Project ID.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_project",
            new { project_id },
            () => Client.DeleteAsync($"projects/{project_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_project_stats", ReadOnly = true)]
    [Description("Gets project statistics.")]
    public Task<string> GetProjectStatsAsync(
        [Description("Project ID.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"projects/{project_id}/stats", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_leave_project")]
    [Description("Leaves the project as the current user.")]
    public Task<string> LeaveProjectAsync(
        [Description("Project ID.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"projects/{project_id}/leave", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_like_project")]
    [Description("Likes a project.")]
    public Task<string> LikeProjectAsync(
        [Description("Project ID.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"projects/{project_id}/like", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unlike_project")]
    [Description("Removes a like from a project.")]
    public Task<string> UnlikeProjectAsync(
        [Description("Project ID.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"projects/{project_id}/unlike", ct: cancellationToken));
    }
}
