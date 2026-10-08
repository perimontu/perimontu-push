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

/// <summary>Cuerpo que se reenvía a la API del proyecto (<c>UrlApi</c>).</summary>
public sealed record ReadPayload(
    string Event,
    string ProjectId,
    string MessageId,
    string DeviceToken,
    DateTimeOffset SentAt,
    DateTimeOffset ReadAt);

/// <summary>Resultado de reenviar el reporte al backend del proyecto.</summary>
public enum ForwardOutcome
{
    Accepted,
    Rejected,
    Unavailable
}
