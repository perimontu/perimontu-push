using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Valores globales de seguridad: cabecera de la API key, proxies de confianza y límite para pedidos sin key válida.</summary>
public sealed class ApiOptions
{
    public const string SECTION = "Api";

    [Required]
    public string KeyHeader { get; set; } = "X-Api-Key";

    /// <summary>Proxies inversos de confianza (p. ej. nginx de Plesk) para leer la IP real de X-Forwarded-For.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Límite por IP para pedidos con proyecto o key desconocidos (frena la adivinación de keys).</summary>
    public RateLimitPolicyOptions UnauthenticatedRateLimit { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}
