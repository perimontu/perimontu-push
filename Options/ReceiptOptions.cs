using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Firma (recibo) que acompaña a cada push y vigencia máxima para aceptar la lectura.</summary>
public sealed class ReceiptOptions
{
    [Required, MinLength(32)]
    public string HmacSecret { get; set; } = string.Empty;

    /// <summary>Días máximos desde el envío para aceptar un reporte de lectura.</summary>
    [Range(1, 3650)]
    public int MaxAgeDays { get; set; } = 30;
}
