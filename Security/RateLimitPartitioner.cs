using System.Threading.RateLimiting;
using CoberPush.Api.Options;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Security;

/// <summary>
/// Define cómo se agrupan los pedidos para el rate limiting: por API key (con el límite de la propia key) o,
/// si no hay una key válida, por IP con un límite estricto que frena la adivinación de keys.
/// </summary>
public static class RateLimitPartitioner
{
    /// <summary>Endpoints protegidos por API key: una cuota por consumidor.</summary>
    public static RateLimitPartition<string> ByKey(HttpContext context, string scope, ApiOptions api)
    {
        var auth = context.RequestServices.GetRequiredService<ApiKeyAuthenticator>().Authenticate(context);

        if (auth is { Project: { } project, Key: { } key })
        {
            return FixedWindow($"{scope}:{project.Id}:{key.Name}", key.RateLimit);
        }

        return Anonymous(context, api);
    }

    /// <summary>Endpoint público de lecturas: una cuota por IP con el límite del proyecto.</summary>
    public static RateLimitPartition<string> ByProjectIp(HttpContext context, ApiOptions api)
    {
        var registry = context.RequestServices.GetRequiredService<IProjectRegistry>();
        var id = context.GetRouteValue(ApiKeyAuthenticator.ROUTE_PARAMETER) as string;

        if (!registry.TryGet(id, out var project))
        {
            return Anonymous(context, api);
        }

        return FixedWindow($"receipts:{project.Id}:{IpOf(context)}", project.ReceiptsRateLimit);
    }

    private static RateLimitPartition<string> Anonymous(HttpContext context, ApiOptions api)
    {
        return FixedWindow($"anon:{IpOf(context)}", api.UnauthenticatedRateLimit);
    }

    private static RateLimitPartition<string> FixedWindow(string partitionKey, RateLimitPolicyOptions policy)
    {
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    private static string IpOf(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
