using CoberPush.Api.Models;
using CoberPush.Api.Security;
using CoberPush.Api.Services;
using CoberPush.Api.Validation;

namespace CoberPush.Api.Endpoints;

/// <summary>Endpoints de envío de un proyecto: API key del proyecto + IP permitida + rate limiting de la key.</summary>
public static class SendEndpoints
{
    public static IEndpointRouteBuilder MapSendEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/{projectId}/push")
            .RequireRateLimiting(RateLimitPolicies.SEND)
            .AddEndpointFilter<ProjectApiKeyEndpointFilter>()
            .AddEndpointFilter<KeyIpEndpointFilter>();

        group.MapPost("/send", SendToTokensAsync);
        group.MapPost("/topic/{topic}", SendToTopicAsync);

        return app;
    }

    private static async Task<IResult> SendToTokensAsync(
        HttpContext http,
        SendPushRequest request,
        PushRequestValidator validator,
        IPushSender sender,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var context = http.GetProjectContext();
        var tokens = request.Tokens?.Distinct().ToList();
        var errors = validator.ValidateTokens(tokens);
        var build = validator.BuildContent(context.Project, request);

        foreach (var (field, messages) in build.Errors)
        {
            errors[field] = messages;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await sender.SendToTokensAsync(context.Project, build.Content!, tokens!, request.DryRun, ct);

        loggerFactory.CreateLogger("Push").LogInformation(
            "Envío {MessageId} ({Project}/{Consumer}): {Ok} ok, {Failed} con error (dryRun={DryRun})",
            result.MessageId, context.Project.Id, context.Key.Name, result.SuccessCount, result.FailureCount, request.DryRun);

        return Results.Ok(result);
    }

    private static async Task<IResult> SendToTopicAsync(
        HttpContext http,
        string topic,
        PushContentRequest request,
        PushRequestValidator validator,
        IPushSender sender,
        CancellationToken ct)
    {
        var context = http.GetProjectContext();
        var errors = validator.ValidateTopic(topic);
        var build = validator.BuildContent(context.Project, request);

        foreach (var (field, messages) in build.Errors)
        {
            errors[field] = messages;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return Results.Ok(await sender.SendToTopicAsync(context.Project, build.Content!, topic, request.DryRun, ct));
    }
}
