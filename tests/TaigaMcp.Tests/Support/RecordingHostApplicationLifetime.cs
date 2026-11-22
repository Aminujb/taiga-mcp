using Microsoft.Extensions.Hosting;

namespace TaigaMcp.Tests.Support;

/// <summary>
/// An <see cref="IHostApplicationLifetime"/> implementation with controllable tokens,
/// used to trigger Stopping/Stopped events in tests.
/// </summary>
internal sealed class RecordingHostApplicationLifetime : IHostApplicationLifetime
{
    private readonly CancellationToken _stopping;

    public RecordingHostApplicationLifetime(CancellationToken stopping) => _stopping = stopping;

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => _stopping;

    public CancellationToken ApplicationStopped => _stopping;

    public bool StopRequested { get; private set; }

    public void StopApplication() => StopRequested = true;
}
