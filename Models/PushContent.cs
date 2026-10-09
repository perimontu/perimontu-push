namespace CoberPush.Api.Models;

/// <summary>Contenido ya validado y firmado, listo para convertirse en mensaje FCM.</summary>
public sealed record PushContent(
    string MessageId,
    string Title,
    string Body,
    string? Url,
    string? ImageUrl,
    long SentAt,
    string Receipt,
    IReadOnlyDictionary<string, string> Data,
    int TtlSeconds);

/// <summary>Destinatario ya validado: token y el messageId de su envío.</summary>
public sealed record PushRecipient(string Token, string MessageId);

/// <summary>Mensaje a un token: el contenido ya personalizado con su messageId y su recibo.</summary>
public sealed record TokenMessage(string Token, PushContent Content);
