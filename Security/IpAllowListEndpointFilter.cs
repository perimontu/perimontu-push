namespace CoberPush.Api.Security;

/// <summary>Solo deja pasar pedidos cuya IP (ya resuelta desde X-Forwarded-For si hay proxy de confianza) está permitida.</summary>
public sealed class IpAllowListEndpointFilter(IpAllowList allowList, ILogger<IpAllowListEndpointFilter> logger)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress;

        if (!allowList.Contains(ip))
        {
            logger.LogWarning("Pedido de envío rechazado: IP no permitida {Ip}", ip);
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Prohibido");
        }

        return await next(context);
    }
}
