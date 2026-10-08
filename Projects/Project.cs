using CoberPush.Api.Options;
using CoberPush.Api.Security;

namespace CoberPush.Api.Projects;

/// <summary>Proyecto ya resuelto desde la configuración (inmutable): todo lo necesario para atender sus pedidos.</summary>
public sealed class Project
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required bool Enabled { get; init; }

    public required string FirebaseProjectId { get; init; }

    /// <summary>Nombre de la variable de entorno que contiene la ruta al JSON de Firebase.</summary>
    public required string CredentialsEnvVar { get; init; }

    public required IReadOnlyList<ProjectApiKey> ApiKeys { get; init; }

    public required RateLimitPolicyOptions DefaultRateLimit { get; init; }

    public required RateLimitPolicyOptions ReceiptsRateLimit { get; init; }

    public required byte[] ReceiptSecret { get; init; }

    public required int ReceiptMaxAgeDays { get; init; }

    public required UrlApiOptions UrlApi { get; init; }

    public required UrlPolicyOptions UrlPolicy { get; init; }
}

/// <summary>
/// API key de un consumidor. Solo se conserva su hash SHA-256 en memoria; las IPs y el límite ya vienen resueltos
/// (los propios de la key o, si no define, los del proyecto).
/// </summary>
public sealed class ProjectApiKey
{
    public required string Name { get; init; }

    public required bool Enabled { get; init; }

    public required DateTimeOffset? ExpiresAt { get; init; }

    public required byte[] KeyHash { get; init; }

    public required IpAllowList AllowedIps { get; init; }

    public required RateLimitPolicyOptions RateLimit { get; init; }

    public bool IsActive(DateTimeOffset now)
    {
        return Enabled && (ExpiresAt is null || now < ExpiresAt);
    }
}
