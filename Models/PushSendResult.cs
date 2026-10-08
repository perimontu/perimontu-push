namespace CoberPush.Api.Models;

/// <summary>Resultado de un envío. <c>Success</c> significa "FCM aceptó el mensaje", no que llegó al dispositivo.</summary>
public sealed record PushSendResult(
    string ProjectId,
    string MessageId,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<PushTargetResult> Results);

/// <summary>Resultado por destino. <c>Target</c> es el token enmascarado (nunca completo) o el topic.</summary>
public sealed record PushTargetResult(
    string Target,
    bool Success,
    string? FcmMessageId,
    string? ErrorCode,
    bool RemoveToken);
