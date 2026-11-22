using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Tools for managing Taiga project roles.</summary>
[McpServerToolType]
public sealed class RoleTools : TaigaToolBase
{
    public RoleTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_roles", ReadOnly = true)]
    [Description("Lists roles. Can be filtered by project.")]
    public Task<string> ListRolesAsync(
        [Description("Project ID.")] int? project = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?> { ["project"] = Q(project) };
            return await Client.GetAsync("roles", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_role", ReadOnly = true)]
    [Description("Gets a role by ID.")]
    public Task<string> GetRoleAsync(
        [Description("Role ID.")] int role_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"roles/{role_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_role")]
    [Description("Creates a role in a project. name and project are required.")]
    public Task<string> CreateRoleAsync(
        [Description("Project ID (required).")] int project,
        [Description("Role name (required).")] string name,
        [Description("Role order.")] int? order = null,
        [Description("The role counts toward estimates (true/false).")] bool? computable = null,
        [Description("List of permissions, for example: view_us, view_project, add_us, etc.")] string[]? permissions = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("name", name),
                ("order", order),
                ("computable", computable),
                ("permissions", permissions));
            return Client.PostAsync("roles", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_role")]
    [Description("Updates a role. PATCH by default; replace_all=true uses PUT.")]
    public Task<string> UpdateRoleAsync(
        [Description("Role ID.")] int role_id,
        [Description("Current object version for OCC (optional).")] int? version = null,
        [Description("Replace the whole object (PUT) instead of a partial update (PATCH).")] bool replace_all = false,
        [Description("New role name.")] string? name = null,
        [Description("Role order.")] int? order = null,
        [Description("The role counts toward estimates (true/false).")] bool? computable = null,
        [Description("List of permissions.")] string[]? permissions = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("name", name),
                ("order", order),
                ("computable", computable),
                ("permissions", permissions));
            var path = $"roles/{role_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_role", Destructive = true)]
    [Description("Deletes a role. move_to can give the ID of a role to reassign its users to.")]
    public Task<string> DeleteRoleAsync(
        [Description("Role ID.")] int role_id,
        [Description("ID of the role to reassign the deleted role's users to.")] int? move_to = null,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_role",
            new { role_id, move_to },
            () =>
            {
                var query = new Dictionary<string, string?> { ["moveTo"] = Q(move_to) };
                return Client.DeleteAsync($"roles/{role_id}", query, cancellationToken);
            });
    }
}
