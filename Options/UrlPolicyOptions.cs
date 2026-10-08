using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Hosts y ruta permitidos para la URL que se abre al tocar la notificación.</summary>
public sealed class UrlPolicyOptions
{
    [Required, MinLength(1)]
    public string[] AllowedHosts { get; set; } = [];

    [Required]
    public string CanonicalHost { get; set; } = string.Empty;

    [Required]
    public string PathPrefix { get; set; } = "/app";
}
