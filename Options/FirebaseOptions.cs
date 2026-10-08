using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

public sealed class FirebaseOptions
{
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Nombre de la variable de entorno cuyo valor es la RUTA al JSON de la cuenta de servicio.</summary>
    [Required]
    public string CredentialsEnvVar { get; set; } = string.Empty;
}
