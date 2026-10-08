using CoberPush.Api.Models;
using CoberPush.Api.Security;
using CoberPush.Api.Services;
using CoberPush.Api.Validation;

namespace CoberPush.Api.Endpoints;

/// <summary>Endpoints de envío: protegidos por IP permitida + API key + rate limiting.</summary>
public static class SendEndpoints
{
    public static IEndpointRouteBuilder MapSendEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/push")
            .RequireRateLimiting(RateLimitPolicies.SEND)
            .AddEndpointFilter<IpAllowListEndpointFilter>()
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapPost("/send", SendToTokensAsync);
        group.MapPost("/topic/{topic}", SendToTopicAsync);

        return app;
    }

    private static async Task<IResult> SendToTokensAsync(
        SendPushRequest request,
        PushRequestValidator validator,
        IPushSender sender,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var tokens = request.Tokens?.Distinct().ToList();
        var errors = validator.ValidateTokens(tokens);
        var build = validator.BuildContent(request);

        foreach (var (field, messages) in build.Errors)
        {
            errors[field] = messages;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await sender.SendToTokensAsync(build.Content!, tokens!, request.DryRun, ct);

        loggerFactory.CreateLogger("Push").LogInformation(
            "Envío {MessageId}: {Ok} ok, {Failed} con error (dryRun={DryRun})",
            result.MessageId, result.SuccessCount, result.FailureCount, request.DryRun);

        return Results.Ok(result);
    }

    private static async Task<IResult> SendToTopicAsync(
        string topic,
        PushContentRequest request,
        PushRequestValidator validator,
        IPushSender sender,
        CancellationToken ct)
    {
        var errors = validator.ValidateTopic(topic);
        var build = validator.BuildContent(request);

        foreach (var (field, messages) in build.Errors)
        {
            errors[field] = messages;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return Results.Ok(await sender.SendToTopicAsync(build.Content!, topic, request.DryRun, ct));
    }
}
