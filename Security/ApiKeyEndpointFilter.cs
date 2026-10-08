using System.Security.Cryptography;
using System.Text;
using CoberPush.Api.Options;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Security;

/// <summary>Exige la API key en la cabecera configurada. Compara en tiempo constante.</summary>
public sealed class ApiKeyEndpointFilter(IOptions<ApiOptions> options, ILogger<ApiKeyEndpointFilter> logger)
    : IEndpointFilter
{
    private readonly byte[] _expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.Key));
    private readonly string _header = options.Value.KeyHeader;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers[_header].ToString();

        // Se comparan hashes (longitud fija) para no filtrar la longitud de la clave.
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));

        if (string.IsNullOrEmpty(provided) || !CryptographicOperations.FixedTimeEquals(_expectedHash, providedHash))
        {
            logger.LogWarning("Pedido de envío rechazado: API key ausente o inválida");
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No autorizado");
        }

        return await next(context);
    }
}
