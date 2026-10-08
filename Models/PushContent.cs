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
