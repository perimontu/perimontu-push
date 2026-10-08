namespace CoberPush.Api.Security;

/// <summary>
/// Solo deja pasar pedidos cuya IP (ya resuelta desde X-Forwarded-For si hay proxy de confianza) está permitida
/// para la API key del pedido (las propias de la key o, si no define, las del proyecto).
/// </summary>
public sealed class KeyIpEndpointFilter(ILogger<KeyIpEndpointFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var projectContext = context.HttpContext.GetProjectContext();
        var ip = context.HttpContext.Connection.RemoteIpAddress;

        if (!projectContext.Key.AllowedIps.Contains(ip))
        {
            logger.LogWarning("Pedido rechazado: IP no permitida {Ip} (proyecto {Project}, consumidor {Consumer})",
                ip, projectContext.Project.Id, projectContext.Key.Name);

            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Prohibido");
        }

        return await next(context);
    }
}
