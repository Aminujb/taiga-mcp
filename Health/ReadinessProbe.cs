using Microsoft.Extensions.DependencyInjection;
using TaigaMcp.Configuration;
using TaigaMcp.Taiga;

namespace TaigaMcp.Health;

/// <summary>
/// Caching readiness probe.
///
/// The <c>/health/ready</c> endpoint used to make a real <c>users/me</c> request on every
/// poll: at a 5-second interval that is about 17,000 requests a day to Taiga. The result is
/// now cached, and repeat checks no older than <see cref="HealthOptions.ReadyCacheSeconds"/>
/// return the saved state, with the real request made at most once
/// per interval (protected against parallel checks).
/// </summary>
public sealed class ReadinessProbe : IDisposable
{
    private sealed record Snapshot(HealthResult Result, DateTimeOffset CheckedAt);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaigaOptions _options;
    private readonly int _cacheSeconds;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;
    private bool _disposed;

    public ReadinessProbe(IServiceScopeFactory scopeFactory, TaigaOptions options, HealthOptions health)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _cacheSeconds = Math.Max(0, health.ReadyCacheSeconds);
    }

    /// <summary>Returns the readiness state, refreshing it at most once per interval.</summary>
    public async Task<HealthResult> GetAsync(CancellationToken ct = default)
    {
        if (TryGetFreshSnapshot(out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (TryGetFreshSnapshot(out cached))
            {
                return cached;
            }

            // The client is created through a scope to respect the IHttpClientFactory lifecycle.
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<TaigaClient>();
            var result = await HealthChecks.CheckReadyAsync(client, _options, ct).ConfigureAwait(false);

            Volatile.Write(ref _snapshot, new Snapshot(result, DateTimeOffset.UtcNow));
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetFreshSnapshot(out HealthResult result)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is not null &&
            _cacheSeconds > 0 &&
            DateTimeOffset.UtcNow - snapshot.CheckedAt < TimeSpan.FromSeconds(_cacheSeconds))
        {
            result = snapshot.Result;
            return true;
        }

        result = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }
}
