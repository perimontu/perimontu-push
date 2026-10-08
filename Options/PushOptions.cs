using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Límites y reglas globales del contenido de las notificaciones.</summary>
public sealed class PushOptions
{
    public const string SECTION = "Push";

    [Range(1, 500)]
    public int MaxTokensPerRequest { get; set; } = 500;

    [Range(1024, 1_048_576)]
    public int MaxBodyBytes { get; set; } = 65_536;

    /// <summary>TTL por defecto en segundos (FCM admite hasta 28 días).</summary>
    [Range(0, 2_419_200)]
    public int DefaultTtlSeconds { get; set; } = 86_400;

    [Required]
    public string AndroidChannelId { get; set; } = "cober_general";
}
