using CoberPush.Api.Models;

namespace CoberPush.Api.Services;

/// <summary>Abstracción del envío (permite probar los endpoints sin Firebase).</summary>
public interface IPushSender
{
    Task<PushSendResult> SendToTokensAsync(
        PushContent content, IReadOnlyList<string> tokens, bool dryRun, CancellationToken ct = default);

    Task<PushSendResult> SendToTopicAsync(
        PushContent content, string topic, bool dryRun, CancellationToken ct = default);
}
