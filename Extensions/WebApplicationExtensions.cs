using CoberPush.Api.Endpoints;
using CoberPush.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace CoberPush.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>Pipeline: IP real (proxy) → manejo de errores → HSTS → límite de cuerpo → rate limiting → endpoints.</summary>
    public static WebApplication UseCoberPush(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler(handler => handler.Run(WriteServerError));

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments(ReceiptEndpoints.PATH),
            branch => branch.Use(LimitReceiptBody));

        app.UseRateLimiter();

        app.MapHealthEndpoints();
        app.MapSendEndpoints();
        app.MapReceiptEndpoints();

        return app;
    }

    /// <summary>El endpoint público de lecturas acepta cuerpos muy pequeños.</summary>
    private static Task LimitReceiptBody(HttpContext context, RequestDelegate next)
    {
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = ReceiptEndpoints.MAX_BODY_BYTES;
        }

        return next(context);
    }

    /// <summary>
    /// Respuesta genérica: nunca se exponen detalles de la excepción. Única excepción: Firebase sin credencial,
    /// que se informa como 503 accionable (solo lo ven quienes pasaron IP y API key).
    /// </summary>
    private static Task WriteServerError(HttpContext context)
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (error is FirebaseUnavailableException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Firebase no disponible: revise la credencial (GOOGLE_APPLICATION_CREDENTIALS)")
                .ExecuteAsync(context);
        }

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Error interno")
            .ExecuteAsync(context);
    }
}
