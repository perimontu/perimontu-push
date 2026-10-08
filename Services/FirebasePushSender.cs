using CoberPush.Api.Models;
using CoberPush.Api.Projects;
using FirebaseAdmin.Messaging;

namespace CoberPush.Api.Services;

/// <summary>Envía por FCM usando el cliente del proyecto indicado en cada llamada.</summary>
public sealed class FirebasePushSender(
    PushMessageFactory factory,
    IFirebaseMessagingProvider provider,
    ILogger<FirebasePushSender> logger) : IPushSender
{
    private const string HEALTH_TOPIC = "healthcheck";

    public async Task<PushSendResult> SendToTokensAsync(
        Project project, PushContent content, IReadOnlyList<string> tokens, bool dryRun, CancellationToken ct = default)
    {
        var message = factory.CreateForTokens(content, tokens);
        var response = await provider.GetMessaging(project).SendEachForMulticastAsync(message, dryRun, ct);

        var results = response.Responses
            .Select((r, i) => ToTargetResult(TokenMasker.Mask(tokens[i]), r))
            .ToList();

        return BuildResult(project, content.MessageId, results);
    }

    public async Task<PushSendResult> SendToTopicAsync(
        Project project, PushContent content, string topic, bool dryRun, CancellationToken ct = default)
    {
        var message = factory.CreateForTopic(content, topic);
        var messaging = provider.GetMessaging(project);

        try
        {
            var fcmId = await messaging.SendAsync(message, dryRun, ct);
            return BuildResult(project, content.MessageId, [new PushTargetResult($"topic:{topic}", true, fcmId, null, false)]);
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning(ex, "FCM rechazó el envío al topic {Topic} ({Project}): {Code}",
                topic, project.Id, ex.MessagingErrorCode);

            return BuildResult(project, content.MessageId,
                [new PushTargetResult($"topic:{topic}", false, null, FcmErrorMapper.ToCode(ex.MessagingErrorCode), false)]);
        }
    }

    public async Task<bool> CheckConnectivityAsync(Project project, CancellationToken ct = default)
    {
        var messaging = provider.GetMessaging(project);
        var probe = new Message
        {
            Topic = HEALTH_TOPIC,
            Data = new Dictionary<string, string> { ["type"] = "healthcheck" }
        };

        try
        {
            await messaging.SendAsync(probe, dryRun: true, ct);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning(ex, "Health de Firebase fallido ({Project}): {Code}", project.Id, ex.MessagingErrorCode);
            return false;
        }
    }

    private static PushTargetResult ToTargetResult(string target, SendResponse response)
    {
        if (response.IsSuccess)
        {
            return new PushTargetResult(target, true, response.MessageId, null, false);
        }

        var code = response.Exception?.MessagingErrorCode;
        return new PushTargetResult(
            target, false, null, FcmErrorMapper.ToCode(code), FcmErrorMapper.ShouldRemoveToken(code));
    }

    private static PushSendResult BuildResult(Project project, string messageId, IReadOnlyList<PushTargetResult> results)
    {
        var success = results.Count(r => r.Success);
        return new PushSendResult(project.Id, messageId, success, results.Count - success, results);
    }
}
