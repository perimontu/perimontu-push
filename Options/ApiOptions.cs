using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Seguridad de los endpoints de envío (API key + IPs permitidas) y proxies de confianza.</summary>
public sealed class ApiOptions
{
    public const string SECTION = "Api";

    [Required]
    public string KeyHeader { get; set; } = "X-Api-Key";

    /// <summary>Secreto: variable de entorno <c>Api__Key</c> o user-secrets. Nunca en appsettings.json.</summary>
    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    /// <summary>IPs (o CIDR) autorizadas a enviar. Si está vacío, nadie puede enviar.</summary>
    public string[] AllowedSendIps { get; set; } = [];

    /// <summary>Proxies inversos de confianza (p. ej. nginx de Plesk) para leer la IP real de X-Forwarded-For.</summary>
    public string[] KnownProxies { get; set; } = [];
}
