using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Ventana fija de peticiones (se usa por API key, por proyecto y para pedidos sin key válida).</summary>
public sealed class RateLimitPolicyOptions
{
    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 60;

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; } = 60;
}
