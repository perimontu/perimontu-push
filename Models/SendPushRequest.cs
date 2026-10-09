namespace CoberPush.Api.Models;

/// <summary>Contenido común de un pedido de envío (a tokens o a un topic).</summary>
public record PushContentRequest
{
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public string? MessageId { get; init; }
    public Dictionary<string, string>? Data { get; init; }
    public int? TtlSeconds { get; init; }
    public bool DryRun { get; init; }
}

/// <summary>
/// Pedido de envío a tokens FCM. Se usa <c>Tokens</c> (la API genera un messageId por token) o <c>Targets</c>
/// (el llamador define el messageId de cada token); son excluyentes. El <c>MessageId</c> global no aplica aquí.
/// </summary>
public sealed record SendPushRequest : PushContentRequest
{
    public List<string>? Tokens { get; init; }
    public List<PushTargetRequest>? Targets { get; init; }
}

/// <summary>Un destino con su propio messageId (opcional: si falta se genera).</summary>
public sealed record PushTargetRequest
{
    public string? Token { get; init; }
    public string? MessageId { get; init; }
}
