using CoberPush.Api.Models;
using CoberPush.Api.Options;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Services;

/// <summary>
/// Envía por FCM. La app de Firebase se crea de forma perezosa (primer envío) con las credenciales por defecto
/// (<c>GOOGLE_APPLICATION_CREDENTIALS</c>); así la API arranca y responde /health aunque la credencial falte.
/// </summary>
public sealed class FirebasePushSender : IPushSender
{
    private readonly PushMessageFactory _factory;
    private readonly ILogger<FirebasePushSender> _logger;
    private readonly Lazy<FirebaseMessaging> _messaging;

    public FirebasePushSender(
        PushMessageFactory factory, IOptions<FirebaseOptions> firebaseOptions, ILogger<FirebasePushSender> logger)
    {
        _factory = factory;
        _logger = logger;
        _messaging = new Lazy<FirebaseMessaging>(
            () => CreateMessaging(firebaseOptions.Value.ProjectId), LazyThreadSafetyMode.PublicationOnly);
    }

    public async Task<PushSendResult> SendToTokensAsync(
        PushContent content, IReadOnlyList<string> tokens, bool dryRun, CancellationToken ct = default)
    {
        var message = _factory.CreateForTokens(content, tokens);
        var response = await _messaging.Value.SendEachForMulticastAsync(message, dryRun, ct);

        var results = response.Responses
            .Select((r, i) => ToTargetResult(TokenMasker.Mask(tokens[i]), r))
            .ToList();

        return BuildResult(content.MessageId, results);
    }

    public async Task<PushSendResult> SendToTopicAsync(
        PushContent content, string topic, bool dryRun, CancellationToken ct = default)
    {
        var message = _factory.CreateForTopic(content, topic);

        try
        {
            var fcmId = await _messaging.Value.SendAsync(message, dryRun, ct);
            return BuildResult(content.MessageId, [new PushTargetResult($"topic:{topic}", true, fcmId, null, false)]);
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogWarning(ex, "FCM rechazó el envío al topic {Topic}: {Code}", topic, ex.MessagingErrorCode);
            var code = ex.MessagingErrorCode;
            return BuildResult(content.MessageId,
                [new PushTargetResult($"topic:{topic}", false, null, FcmErrorMapper.ToCode(code), false)]);
        }
    }

    private static FirebaseMessaging CreateMessaging(string projectId)
    {
        try
        {
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.GetApplicationDefault(),
                ProjectId = projectId
            });

            return FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception ex) when (ex is InvalidOperationException or AggregateException or IOException
            or System.Text.Json.JsonException or ArgumentException)
        {
            throw new FirebaseUnavailableException(
                "No se pudo inicializar Firebase: revise GOOGLE_APPLICATION_CREDENTIALS.", ex);
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

    private static PushSendResult BuildResult(string messageId, IReadOnlyList<PushTargetResult> results)
    {
        var success = results.Count(r => r.Success);
        return new PushSendResult(messageId, success, results.Count - success, results);
    }
}
