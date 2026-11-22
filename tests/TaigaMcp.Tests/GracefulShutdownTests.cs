using TaigaMcp.Configuration;
using TaigaMcp.Hosting;
using TaigaMcp.Tests.Support;
using Xunit;

namespace TaigaMcp.Tests;

public class GracefulShutdownTests
{
    [Fact]
    public void HandleFatal_LogsCriticalAndStopsApplication()
    {
        var logger = new CapturingLogger<object>();
        var lifetime = new FakeHostApplicationLifetime();

        GracefulShutdown.HandleFatal(logger, lifetime, new InvalidOperationException("boom"));

        Assert.True(lifetime.StopRequested);
        Assert.Contains(logger.Messages, message =>
            message.Contains("shutdown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HandleFatal_WithoutException_StillStopsApplication()
    {
        var logger = new CapturingLogger<object>();
        var lifetime = new FakeHostApplicationLifetime();

        GracefulShutdown.HandleFatal(logger, lifetime, null);

        Assert.True(lifetime.StopRequested);
    }

    [Fact]
    public void Register_LogsOnStoppingAndStopped()
    {
        var logger = new CapturingLogger<object>();
        using var cts = new CancellationTokenSource();
        var lifetime = new RecordingHostApplicationLifetime(cts.Token);

        // Turn off the global event subscription so other tests aren't affected.
        GracefulShutdown.Register(
            logger,
            lifetime,
            new ShutdownOptions { ShutdownOnUnhandledException = false });

        cts.Cancel();

        Assert.Contains(logger.Messages, message =>
            message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message =>
            message.Contains("stopped", StringComparison.OrdinalIgnoreCase));
    }
}
