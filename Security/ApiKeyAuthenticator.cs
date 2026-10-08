using System.Security.Cryptography;
using System.Text;
using CoberPush.Api.Options;
using CoberPush.Api.Projects;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Security;

/// <summary>Proyecto y consumidor (API key) ya autenticados para el pedido en curso.</summary>
public sealed record ProjectContext(Project Project, ProjectApiKey Key);

/// <summary>Resultado de autenticar un pedido. <c>Key</c> es nulo si no hay una key válida, habilitada y vigente.</summary>
public sealed record AuthenticationResult(Project? Project, ProjectApiKey? Key);

/// <summary>
/// Resuelve el proyecto por el <c>{projectId}</c> de la ruta y busca la key de la cabecera entre las de ESE proyecto
/// (una key de un proyecto nunca vale en otro). Compara hashes en tiempo constante.
/// </summary>
public sealed class ApiKeyAuthenticator(IProjectRegistry registry, IOptions<ApiOptions> options, TimeProvider clock)
{
    public const string ROUTE_PARAMETER = "projectId";

    private readonly string _header = options.Value.KeyHeader;

    public AuthenticationResult Authenticate(HttpContext context)
    {
        registry.TryGet(context.GetRouteValue(ROUTE_PARAMETER) as string, out var project);

        var provided = context.Request.Headers[_header].ToString();

        if (project is null || string.IsNullOrEmpty(provided))
        {
            return new AuthenticationResult(project, null);
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return new AuthenticationResult(project, FindActiveKey(project, providedHash));
    }

    /// <summary>Recorre todas las keys sin cortar al primer acierto para no filtrar posiciones por tiempo.</summary>
    private ProjectApiKey? FindActiveKey(Project project, byte[] providedHash)
    {
        var now = clock.GetUtcNow();
        ProjectApiKey? match = null;

        foreach (var key in project.ApiKeys)
        {
            if (CryptographicOperations.FixedTimeEquals(key.KeyHash, providedHash) && key.IsActive(now))
            {
                match = key;
            }
        }

        return match;
    }
}
