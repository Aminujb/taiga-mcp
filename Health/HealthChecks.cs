using TaigaMcp.Configuration;
using TaigaMcp.Taiga;

namespace TaigaMcp.Health;

/// <summary>Result of a readiness check.</summary>
public readonly record struct HealthResult(bool IsReady, string? Reason)
{
    public static HealthResult Ready() => new(true, null);

    public static HealthResult NotReady(string reason) => new(false, reason);
}

/// <summary>Server health check logic (kept separate so it can be tested).</summary>
public static class HealthChecks
{
    /// <summary>
    /// Readiness check: the Taiga configuration is valid and the API can be reached
    /// with the current credentials. Uses the lightweight users/me request.
    /// </summary>
    public static async Task<HealthResult> CheckReadyAsync(
        TaigaClient client,
        TaigaOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl) ||
            !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return HealthResult.NotReady("BaseUrl is not set or has an invalid format.");
        }

        try
        {
            await client.GetAsync("users/me", disablePagination: null, ct: ct).ConfigureAwait(false);
            return HealthResult.Ready();
        }
        catch (Exception ex)
        {
            return HealthResult.NotReady(ex.Message);
        }
    }
}
