using CoberPush.Api.Models;
using CoberPush.Api.Projects;
using CoberPush.Api.Services;
using CoberPush.Api.Validation;

namespace CoberPush.Api.Endpoints;

/// <summary>Límite de tamaño de cuerpo propio de un endpoint (lo aplica un middleware, ver <c>UseCoberPush</c>).</summary>
public sealed record BodyLimitMetadata(long MaxBytes);

/// <summary>
/// Endpoints para las apps instaladas (públicos: sin filtro de IP). Se protegen con la firma del recibo
/// (que incluye el proyecto), rate limiting por IP y un límite de tamaño de cuerpo pequeño.
/// </summary>
public static class ReceiptEndpoints
{
    public const int MAX_BODY_BYTES = 8 * 1024;

    private const int MAX_CLOCK_SKEW_SECONDS = 300;

    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/{projectId}/receipts")
            .RequireRateLimiting(RateLimitPolicies.RECEIPTS)
            .WithMetadata(new BodyLimitMetadata(MAX_BODY_BYTES));

        group.MapPost("/read", ReadAsync);

        return app;
    }

    private static async Task<IResult> ReadAsync(
        string projectId,
        ReadReceiptRequest request,
        IProjectRegistry registry,
        ReadReceiptValidator validator,
        IReceiptSigner signer,
        IReadForwarder forwarder,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        // Proyecto inexistente o deshabilitado: 404 (el endpoint es público, no hay API key que justifique otro código).
        if (!registry.TryGet(projectId, out var project) || !project.Enabled)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado");
        }

        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var verification = signer.Verify(project, request.MessageId!, request.SentAt, request.Receipt!);
        if (verification != ReceiptVerification.Valid)
        {
            return RejectReceipt(verification);
        }

        var outcome = await forwarder.ForwardReadAsync(project, BuildPayload(project, request, clock.GetUtcNow()), ct);

        loggerFactory.CreateLogger("Receipts").LogInformation(
            "Lectura de {MessageId} ({Project}, token {Token}): {Outcome}",
            request.MessageId, project.Id, TokenMasker.Mask(request.DeviceToken), outcome);

        return ToResult(outcome);
    }

    private static IResult RejectReceipt(ReceiptVerification verification)
    {
        return verification == ReceiptVerification.Expired
            ? Results.Problem(statusCode: StatusCodes.Status410Gone, title: "El mensaje es demasiado antiguo")
            : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Recibo inválido");
    }

    private static ReadPayload BuildPayload(Project project, ReadReceiptRequest request, DateTimeOffset now)
    {
        var readAt = request.ReadAt is { } at && at <= now.AddSeconds(MAX_CLOCK_SKEW_SECONDS) ? at : now;

        return new ReadPayload(
            "read",
            project.Id,
            request.MessageId!,
            request.DeviceToken!,
            DateTimeOffset.FromUnixTimeSeconds(request.SentAt),
            readAt);
    }

    private static IResult ToResult(ForwardOutcome outcome)
    {
        return outcome switch
        {
            ForwardOutcome.Accepted => Results.Accepted(),
            ForwardOutcome.Unavailable => Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable, title: "Servicio no disponible, reintente luego"),
            _ => Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "El backend rechazó el reporte")
        };
    }
}
