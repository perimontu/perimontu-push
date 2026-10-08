using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Conexión con la API del proyecto que recibe los reportes de lectura.</summary>
public sealed class UrlApiOptions
{
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string ReadPath { get; set; } = "/read";

    [Required, MinLength(8)]
    public string BearerToken { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(0, 5)]
    public int RetryCount { get; set; } = 2;
}
