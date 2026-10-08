namespace CoberPush.Api.Security;

/// <summary>
/// Exige proyecto existente + API key válida del proyecto (<c>401</c> uniforme para no revelar qué proyectos existen)
/// y proyecto habilitado (<c>403</c>). Deja el <see cref="ProjectContext"/> para el handler.
/// </summary>
public sealed class ProjectApiKeyEndpointFilter(ApiKeyAuthenticator authenticator, ILogger<ProjectApiKeyEndpointFilter> logger)
    : IEndpointFilter
{
    private const int MAX_LOGGED_ID_LENGTH = 32;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var result = authenticator.Authenticate(http);

        if (result.Project is null || result.Key is null)
        {
            logger.LogWarning("Pedido rechazado: proyecto o API key inválidos (proyecto '{ProjectId}')", RequestedId(http));
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No autorizado");
        }

        if (!result.Project.Enabled)
        {
            logger.LogWarning("Pedido rechazado: proyecto {Project} deshabilitado", result.Project.Id);
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Prohibido");
        }

        http.SetProjectContext(new ProjectContext(result.Project, result.Key));
        return await next(context);
    }

    /// <summary>El id viene del cliente: se recorta antes de loguearlo.</summary>
    private static string RequestedId(HttpContext context)
    {
        var id = context.GetRouteValue(ApiKeyAuthenticator.ROUTE_PARAMETER) as string ?? string.Empty;
        return id.Length > MAX_LOGGED_ID_LENGTH ? id[..MAX_LOGGED_ID_LENGTH] : id;
    }
}
