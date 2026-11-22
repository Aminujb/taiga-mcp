using System.Net;
using System.Text.Json.Nodes;
using TaigaMcp.Tests.Support;
using TaigaMcp.Tools;
using Xunit;

namespace TaigaMcp.Tests;

public class CommentToolsTests
{
    private const string History = """
        [
          {"id":"c","created_at":"2026-03-01T10:00:00Z","comment":"second","delete_comment_date":null,
           "edit_comment_date":"2026-03-02T10:00:00Z",
           "user":{"name":"Ada","username":"ada","photo":"https://media.test/p.jpg?token=secret"}},
          {"id":"b","created_at":"2026-02-01T10:00:00Z","comment":"","delete_comment_date":null,
           "user":{"name":"Ada","username":"ada"}},
          {"id":"d","created_at":"2026-02-15T10:00:00Z","comment":"removed","delete_comment_date":"2026-02-16T10:00:00Z",
           "user":{"name":"Ada","username":"ada"}},
          {"id":"a","created_at":"2026-01-01T10:00:00Z","comment":"first","delete_comment_date":null,
           "user":{"name":"Grace","username":"grace"}}
        ]
        """;

    [Fact]
    public async Task ReturnsLiveCommentsOldestFirstWithoutExtraFields()
    {
        using var harness = new ToolHarness((_, _) => FakeHttpMessageHandler.Json(HttpStatusCode.OK, History));
        var tools = new CommentTools(harness.Client, harness.Audit, harness.Metrics);

        var result = await tools.ListUserStoryCommentsAsync(42);

        var comments = Assert.IsType<JsonArray>(JsonNode.Parse(result));
        Assert.Equal(2, comments.Count);
        Assert.Equal("first", comments[0]!["comment"]!.GetValue<string>());
        Assert.Equal("grace", comments[0]!["username"]!.GetValue<string>());
        Assert.Equal("second", comments[1]!["comment"]!.GetValue<string>());
        Assert.Equal("2026-03-02T10:00:00Z", comments[1]!["edited_at"]!.GetValue<string>());
        Assert.DoesNotContain("token=secret", result, StringComparison.Ordinal);

        var request = Assert.Single(harness.Handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("history/userstory/42", request.Uri, StringComparison.Ordinal);
        Assert.Equal("True", request.Header("x-disable-pagination"));
    }

    [Fact]
    public async Task EachToolReadsItsOwnTicketType()
    {
        using var harness = new ToolHarness((_, _) => FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));
        var tools = new CommentTools(harness.Client, harness.Audit, harness.Metrics);

        Assert.Equal("[]", await tools.ListTaskCommentsAsync(7));
        Assert.Equal("[]", await tools.ListIssueCommentsAsync(8));

        Assert.EndsWith("history/task/7", harness.Handler.Requests[0].Uri, StringComparison.Ordinal);
        Assert.EndsWith("history/issue/8", harness.Handler.Requests[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiErrorIsReturnedAsText()
    {
        using var harness = new ToolHarness((_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.NotFound, """{"detail":"missing"}"""));
        var tools = new CommentTools(harness.Client, harness.Audit, harness.Metrics);

        var result = await tools.ListIssueCommentsAsync(9);

        Assert.Contains("404", result, StringComparison.Ordinal);
        Assert.Contains("missing", result, StringComparison.Ordinal);
    }
}
