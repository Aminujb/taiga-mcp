using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>One item in a bulk member invitation.</summary>
public sealed class BulkMembership
{
    [Description("ID of the role assigned to the member.")]
    public int role_id { get; set; }

    [Description("Username or email of the invited user.")]
    public string username { get; set; } = string.Empty;

    [Description("Additional invitation text (optional).")]
    public string? invitation_extra_text { get; set; }
}

/// <summary>Tools for managing memberships and invitations.</summary>
[McpServerToolType]
public sealed class MembershipTools : TaigaToolBase
{
    public MembershipTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_memberships", ReadOnly = true)]
    [Description("Lists members/invitations. Can be filtered by project and role.")]
    public Task<string> ListMembershipsAsync(
        [Description("Project ID.")] int? project = null,
        [Description("Role ID.")] int? role = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["role"] = Q(role),
            };
            return await Client.GetAsync("memberships", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_membership", ReadOnly = true)]
    [Description("Gets a member/invitation by ID.")]
    public Task<string> GetMembershipAsync(
        [Description("Member ID.")] int membership_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"memberships/{membership_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_membership")]
    [Description("Invites a user to a project (creates a membership). All parameters are required.")]
    public Task<string> CreateMembershipAsync(
        [Description("Project ID (required).")] int project,
        [Description("ID of the role assigned to the member (required).")] int role,
        [Description("Username or email of the invited user (required).")] string username,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("role", role),
                ("username", username));
            return Client.PostAsync("memberships", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_bulk_create_memberships")]
    [Description("Invites users to a project in bulk.")]
    public Task<string> BulkCreateMembershipsAsync(
        [Description("Project ID (required).")] int project_id,
        [Description("List of invitees: role_id, username, invitation_extra_text (required).")] BulkMembership[] bulk_memberships,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("bulk_memberships", bulk_memberships));
            return Client.PostAsync("memberships/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_membership")]
    [Description("Updates a member/invitation (for example changes the role).")]
    public Task<string> UpdateMembershipAsync(
        [Description("Member ID.")] int membership_id,
        [Description("New role ID.")] int? role = null,
        [Description("Current object version for OCC (optional).")] int? version = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("role", role),
                ("version", version));
            return Client.PatchAsync($"memberships/{membership_id}", body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_membership", Destructive = true)]
    [Description("Removes a member or cancels an invitation.")]
    public Task<string> DeleteMembershipAsync(
        [Description("Member/invitation ID.")] int membership_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_membership",
            new { membership_id },
            () => Client.DeleteAsync($"memberships/{membership_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_resend_invitation")]
    [Description("Resends an invitation to a member.")]
    public Task<string> ResendInvitationAsync(
        [Description("Member/invitation ID.")] int membership_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"memberships/{membership_id}/resend_invitation", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_invitation", ReadOnly = true)]
    [Description("Gets an invitation by its token (UUID).")]
    public Task<string> GetInvitationAsync(
        [Description("Invitation UUID.")] string invitation_uuid,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"invitations/{invitation_uuid}", disablePagination: null, ct: cancellationToken));
    }
}
