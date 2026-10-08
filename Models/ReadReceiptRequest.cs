namespace CoberPush.Api.Models;

/// <summary>Reporte de lectura enviado por la app instalada al tocar la notificación.</summary>
public sealed record ReadReceiptRequest
{
    public string? MessageId { get; init; }

    /// <summary>Fecha de envío (Unix, segundos) tal como llegó en <c>data.sentAt</c>.</summary>
    public long SentAt { get; init; }

    /// <summary>Firma recibida en <c>data.receipt</c>.</summary>
    public string? Receipt { get; init; }

    public string? DeviceToken { get; init; }

    public DateTimeOffset? ReadAt { get; init; }
}

/// <summary>Cuerpo que se reenvía a la API PHP.</summary>
public sealed record PhpReadPayload(
    string Event,
    string MessageId,
    string DeviceToken,
    DateTimeOffset SentAt,
    DateTimeOffset ReadAt);

/// <summary>Resultado de reenviar el reporte a PHP.</summary>
public enum ForwardOutcome
{
    Accepted,
    Rejected,
    Unavailable
}
