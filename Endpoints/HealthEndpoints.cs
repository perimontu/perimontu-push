using CoberPush.Api.Security;
using CoberPush.Api.Services;

namespace CoberPush.Api.Endpoints;

public static class HealthEndpoints
{
    public const string PATH = "/api/{projectId}/health";

    /// <summary>
    /// <c>/health</c>: sin autenticación, solo indica que el proceso vive (no revela configuración).
    /// <c>/api/{projectId}/health</c>: con API key del proyecto; comprueba la conexión con Firebase de ese proyecto.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet(PATH, ProjectHealthAsync)
            .RequireRateLimiting(RateLimitPolicies.HEALTH)
            .AddEndpointFilter<ProjectApiKeyEndpointFilter>();

        return app;
    }

    private static async Task<IResult> ProjectHealthAsync(
        HttpContext http, IProjectHealthChecker checker, CancellationToken ct)
    {
        var project = http.GetProjectContext().Project;
        var result = await checker.CheckAsync(project, ct);

        var body = new
        {
            projectId = project.Id,
            status = result.FirebaseOk ? "ok" : "error",
            firebase = result.FirebaseOk ? "ok" : "error",
            checkedAt = result.CheckedAt
        };

        return result.FirebaseOk ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
