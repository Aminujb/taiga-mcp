namespace TaigaMcp.Health;

/// <summary>
/// Registers the health endpoints (not part of MCP):
///  - /health      - general check (compatibility);
///  - /health/live - liveness: the process is alive and responding;
///  - /health/ready- readiness: configured and able to reach the Taiga API
///                   (the result is cached through <see cref="ReadinessProbe"/>).
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Text("ok"));

        app.MapGet("/health/live", () => Results.Text("ok"));

        app.MapGet("/health/ready", async (ReadinessProbe probe, CancellationToken cancellationToken) =>
        {
            var result = await probe.GetAsync(cancellationToken).ConfigureAwait(false);

            return result.IsReady
                ? Results.Json(new { status = "Healthy" })
                : Results.Json(
                    new { status = "Unhealthy", reason = result.Reason },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }
}
