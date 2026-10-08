using CoberPush.Api.Models;
using CoberPush.Api.Services;
using CoberPush.Api.Validation;

namespace CoberPush.Api.Endpoints;

/// <summary>
/// Endpoints para las apps instaladas (públicos: sin filtro de IP). Se protegen con la firma del recibo,
/// rate limiting por IP y un límite de tamaño de cuerpo pequeño.
/// </summary>
public static class ReceiptEndpoints
{
    public const string PATH = "/api/receipts";
    public const int MAX_BODY_BYTES = 8 * 1024;

    private const int MAX_CLOCK_SKEW_SECONDS = 300;

    public static IEndpointRouteBuilder MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PATH).RequireRateLimiting(RateLimitPolicies.RECEIPTS);

        group.MapPost("/read", ReadAsync);

        return app;
    }

    private static async Task<IResult> ReadAsync(
        ReadReceiptRequest request,
        ReadReceiptValidator validator,
        IReceiptSigner signer,
        IPhpReadForwarder forwarder,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var verification = signer.Verify(request.MessageId!, request.SentAt, request.Receipt!);
        if (verification != ReceiptVerification.Valid)
        {
            return RejectReceipt(verification);
        }

        var payload = BuildPayload(request, clock.GetUtcNow());
        var outcome = await forwarder.ForwardReadAsync(payload, ct);

        loggerFactory.CreateLogger("Receipts").LogInformation(
            "Lectura de {MessageId} (token {Token}): {Outcome}",
            request.MessageId, TokenMasker.Mask(request.DeviceToken), outcome);

        return ToResult(outcome);
    }

    private static IResult RejectReceipt(ReceiptVerification verification)
    {
        return verification == ReceiptVerification.Expired
            ? Results.Problem(statusCode: StatusCodes.Status410Gone, title: "El mensaje es demasiado antiguo")
            : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Recibo inválido");
    }

    private static PhpReadPayload BuildPayload(ReadReceiptRequest request, DateTimeOffset now)
    {
        var readAt = request.ReadAt is { } at && at <= now.AddSeconds(MAX_CLOCK_SKEW_SECONDS) ? at : now;

        return new PhpReadPayload(
            "read",
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
