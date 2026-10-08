using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Lista de proyectos (sección <c>Projects</c> de appsettings).</summary>
public sealed class ProjectsOptions
{
    public const string SECTION = "Projects";

    public List<ProjectOptions> Items { get; } = [];
}

/// <summary>Un proyecto: backend de Firebase, consumidores (API keys), backend de destino y reglas propias.</summary>
public sealed class ProjectOptions
{
    /// <summary>Identificador alfanumérico (llega en la ruta de cada pedido).</summary>
    [Required]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    [Required]
    public FirebaseOptions Firebase { get; set; } = new();

    /// <summary>IPs/CIDR que pueden enviar. Cada API key puede sobrescribirlas con las suyas.</summary>
    public string[] AllowedIps { get; set; } = [];

    public List<ApiKeyOptions> ApiKeys { get; set; } = [];

    public RateLimitPolicyOptions DefaultRateLimit { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    public RateLimitPolicyOptions ReceiptsRateLimit { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    [Required]
    public ReceiptOptions Receipts { get; set; } = new();

    [Required]
    public UrlApiOptions UrlApi { get; set; } = new();

    [Required]
    public UrlPolicyOptions UrlPolicy { get; set; } = new();
}

/// <summary>Un consumidor del servicio (por ejemplo, el backend PHP o un panel de administración).</summary>
public sealed class ApiKeyOptions
{
    /// <summary>Nombre interno para identificar al consumidor en los logs.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Fecha y hora (con zona) a partir de la cual la key deja de valer. Vacío = no vence.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Si no está vacío, sobrescribe las IPs permitidas del proyecto para esta key.</summary>
    public string[] AllowedIps { get; set; } = [];

    /// <summary>Si es nulo se usa el <c>DefaultRateLimit</c> del proyecto.</summary>
    public RateLimitPolicyOptions? RateLimit { get; set; }
}
