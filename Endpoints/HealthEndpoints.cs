namespace CoberPush.Api.Endpoints;

public static class HealthEndpoints
{
    /// <summary>Sin autenticación; no revela configuración.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        return app;
    }
}
