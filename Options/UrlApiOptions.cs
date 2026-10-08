using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Conexión con la API PHP que recibe los reportes de lectura.</summary>
public sealed class PhpApiOptions
{
    public const string SECTION = "PhpApi";

    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string ReadPath { get; set; } = "/read";

    /// <summary>Secreto: variable de entorno <c>PhpApi__BearerToken</c> o user-secrets.</summary>
    [Required]
    public string BearerToken { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(0, 5)]
    public int RetryCount { get; set; } = 2;
}
