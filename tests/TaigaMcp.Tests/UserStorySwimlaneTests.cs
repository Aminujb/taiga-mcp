using System.Net;
using TaigaMcp.Tests.Support;
using TaigaMcp.Tools;
using Xunit;

namespace TaigaMcp.Tests;

public class UserStorySwimlaneTests
{
    [Fact]
    public async Task CreateSendsSwimlane()
    {
        using var harness = new ToolHarness((_, _) => FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        var tools = new UserStoryTools(harness.Client, harness.Audit, harness.Metrics);

        await tools.CreateUserStoryAsync(1, "Story", swimlane: 39);

        var request = Assert.Single(harness.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("\"swimlane\":39", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSendsSwimlane()
    {
        using var harness = new ToolHarness((_, _) => FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        var tools = new UserStoryTools(harness.Client, harness.Audit, harness.Metrics);

        await tools.UpdateUserStoryAsync(7, version: 2, swimlane: 39);

        var request = Assert.Single(harness.Handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Contains("\"swimlane\":39", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateLeavesSwimlaneAloneWhenNotGiven()
    {
        using var harness = new ToolHarness((_, _) => FakeHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        var tools = new UserStoryTools(harness.Client, harness.Audit, harness.Metrics);

        await tools.UpdateUserStoryAsync(7, version: 2, status: 3);

        var request = Assert.Single(harness.Handler.Requests);
        Assert.DoesNotContain("swimlane", request.Body, StringComparison.Ordinal);
    }
}
