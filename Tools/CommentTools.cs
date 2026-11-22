using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>
/// Read-only access to ticket comments. Taiga stores comments in a ticket's history,
/// so these tools read the history and return only the comments that still exist.
/// </summary>
[McpServerToolType]
public sealed class CommentTools : TaigaToolBase
{
    private static readonly JsonSerializerOptions OutputOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public CommentTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_userstory_comments", ReadOnly = true)]
    [Description("Lists a user story's comments, oldest first. Takes the story's internal ID (the id field), not its #ref. Deleted comments are left out.")]
    public Task<string> ListUserStoryCommentsAsync(
        [Description("User story ID (not the #ref).")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ListCommentsAsync("userstory", user_story_id, cancellationToken));
    }

    [McpServerTool(Name = "taiga_list_task_comments", ReadOnly = true)]
    [Description("Lists a task's comments, oldest first. Takes the task's internal ID (the id field), not its #ref. Deleted comments are left out.")]
    public Task<string> ListTaskCommentsAsync(
        [Description("Task ID (not the #ref).")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ListCommentsAsync("task", task_id, cancellationToken));
    }

    [McpServerTool(Name = "taiga_list_issue_comments", ReadOnly = true)]
    [Description("Lists an issue's comments, oldest first. Takes the issue's internal ID (the id field), not its #ref. Deleted comments are left out.")]
    public Task<string> ListIssueCommentsAsync(
        [Description("Issue ID (not the #ref).")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => ListCommentsAsync("issue", issue_id, cancellationToken));
    }

    private async Task<string> ListCommentsAsync(string ticketType, int id, CancellationToken ct)
    {
        var response = await Client.GetAsync($"history/{ticketType}/{id}", null, true, ct).ConfigureAwait(false);
        if (JsonNode.Parse(response) is not JsonArray history)
        {
            throw new InvalidOperationException("Taiga returned an unexpected history response.");
        }

        var comments = history
            .OfType<JsonObject>()
            .Where(IsLiveComment)
            .OrderBy(e => Text(e, "created_at"), StringComparer.Ordinal)
            .Select(ToComment)
            .ToArray<JsonNode?>();

        return new JsonArray(comments).ToJsonString(OutputOptions);
    }

    private static bool IsLiveComment(JsonObject entry)
        => !string.IsNullOrWhiteSpace(Text(entry, "comment")) && entry["delete_comment_date"] is null;

    private static JsonObject ToComment(JsonObject entry)
    {
        var user = entry["user"] as JsonObject;
        return new JsonObject
        {
            ["id"] = Text(entry, "id"),
            ["author"] = user is null ? null : Text(user, "name"),
            ["username"] = user is null ? null : Text(user, "username"),
            ["created_at"] = Text(entry, "created_at"),
            ["edited_at"] = Text(entry, "edit_comment_date"),
            ["comment"] = Text(entry, "comment"),
        };
    }

    private static string? Text(JsonObject entry, string name)
        => entry[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
