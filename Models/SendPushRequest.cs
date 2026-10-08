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

/// <summary>Pedido de envío a una lista de tokens FCM (los guarda el backend PHP).</summary>
public sealed record SendPushRequest : PushContentRequest
{
    public List<string>? Tokens { get; init; }
}
