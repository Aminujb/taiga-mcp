using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaigaMcp.Configuration;

namespace TaigaMcp.Hosting;

/// <summary>
/// Graceful server shutdown:
///  - logs stop signals (SIGTERM / Ctrl+C);
///  - starts a shutdown on an unhandled exception (if enabled).
/// The wait timeout is set through <see cref="HostOptions.ShutdownTimeout"/> in Program.
/// </summary>
public static class GracefulShutdown
{
    /// <summary>Default logger category for shutdown events.</summary>
    public const string LoggerCategory = "TaigaMcp.Shutdown";

    /// <summary>Registers handlers for shutdown events and critical errors.</summary>
    public static void Register(
        ILogger logger,
        IHostApplicationLifetime lifetime,
        ShutdownOptions options)
    {
        lifetime.ApplicationStopping.Register(
            () => logger.LogInformation("Stop signal received, shutting down..."));

        lifetime.ApplicationStopped.Register(
            () => logger.LogInformation("Server stopped."));

        if (!options.ShutdownOnUnhandledException)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            HandleFatal(logger, lifetime, eventArgs.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            logger.LogError(
                eventArgs.Exception,
                "Unhandled exception in a task (not fatal).");
            eventArgs.SetObserved();
        };
    }

    /// <summary>
    /// Handles a fatal exception: writes a critical log entry and
    /// starts a graceful application shutdown.
    /// </summary>
    public static void HandleFatal(ILogger logger, IHostApplicationLifetime lifetime, Exception? exception)
    {
        logger.LogCritical(exception, "Unhandled exception: starting server shutdown.");
        lifetime.StopApplication();
    }
}
