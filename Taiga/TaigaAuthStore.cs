using TaigaMcp.Configuration;

namespace TaigaMcp.Taiga;

/// <summary>
/// Taiga token store, registered as a singleton.
///
/// A <see cref="TaigaClient"/> instance is created for each tool call (transient),
/// so the token cache <b>must</b> live outside it: otherwise every call
/// logs in to Taiga again (extra load, audit noise, risk of lockouts).
///
/// Token access is thread-safe: reads use <see cref="Volatile.Read{T}(ref T)"/>,
/// writes use <see cref="Interlocked.Exchange{T}(ref T, T)"/>. This guarantees changes are
/// visible across threads and references are swapped atomically without extra locks.
/// </summary>
public sealed class TaigaAuthStore : IDisposable
{
    private string? _authToken;
    private string? _refreshToken;
    private bool _disposed;

    public TaigaAuthStore(TaigaOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            AuthToken = options.Token;
        }

        RefreshToken = options.RefreshToken;
    }

    /// <summary>Serializes concurrent login and token refresh attempts.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>Current access token (auth_token).</summary>
    public string? AuthToken
    {
        get => Volatile.Read(ref _authToken);
        set => Interlocked.Exchange(ref _authToken, value);
    }

    /// <summary>Current refresh token.</summary>
    public string? RefreshToken
    {
        get => Volatile.Read(ref _refreshToken);
        set => Interlocked.Exchange(ref _refreshToken, value);
    }

    /// <summary>Releases the semaphore. Called by the DI container when the application stops.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Gate.Dispose();
    }
}
