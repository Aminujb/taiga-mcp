using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Tools for managing Taiga users.</summary>
[McpServerToolType]
public sealed class UserTools : TaigaToolBase
{
    public UserTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_users", ReadOnly = true)]
    [Description("Lists users. Can be filtered by project.")]
    public Task<string> ListUsersAsync(
        [Description("Project ID: return only its members.")] int? project = null,
        [Description("Disable pagination and return all records (default true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?> { ["project"] = Q(project) };
            return await Client.GetAsync("users", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_user", ReadOnly = true)]
    [Description("Gets a user by ID.")]
    public Task<string> GetUserAsync(
        [Description("User ID.")] int user_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"users/{user_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_current_user", ReadOnly = true)]
    [Description("Gets the current authenticated user (users/me).")]
    public Task<string> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("users/me", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_register_user")]
    [Description("Registers a new user (public registration). All parameters are required.")]
    public Task<string> RegisterUserAsync(
        [Description("New user's username.")] string username,
        [Description("Password.")] string password,
        [Description("Email.")] string email,
        [Description("Full name.")] string full_name,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("type", "public"),
                ("username", username),
                ("password", password),
                ("email", email),
                ("full_name", full_name),
                ("accepted_terms", true));
            return Client.PostAsync("auth/register", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_user")]
    [Description("Updates a user profile (PATCH).")]
    public Task<string> UpdateUserAsync(
        [Description("User ID.")] int user_id,
        [Description("Current object version for OCC (optional).")] int? version = null,
        [Description("New username.")] string? username = null,
        [Description("New full name.")] string? full_name = null,
        [Description("New email.")] string? email = null,
        [Description("Biography.")] string? bio = null,
        [Description("Interface language (locale ID).")] string? lang = null,
        [Description("Time zone.")] string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("username", username),
                ("full_name", full_name),
                ("email", email),
                ("bio", bio),
                ("lang", lang),
                ("timezone", timezone));
            return Client.PatchAsync($"users/{user_id}", body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_user", Destructive = true)]
    [Description("Deletes a user by ID. Requires administrator rights.")]
    public Task<string> DeleteUserAsync(
        [Description("User ID.")] int user_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_user",
            new { user_id },
            () => Client.DeleteAsync($"users/{user_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_list_user_contacts", ReadOnly = true)]
    [Description("Gets a user's contacts (a list of users).")]
    public Task<string> ListUserContactsAsync(
        [Description("User ID.")] int user_id,
        [Description("Search string for username, full name or email.")] string? q = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var query = new Dictionary<string, string?> { ["q"] = q };
            return Client.GetAsync($"users/{user_id}/contacts", query, disablePagination: null, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_request_password_recovery")]
    [Description("Requests a password recovery for a user.")]
    public Task<string> RequestPasswordRecoveryAsync(
        [Description("Username or email of the user.")] string username,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(("username", username));
            return Client.PostAsync("users/password_recovery", body, ct: cancellationToken);
        });
    }
}
