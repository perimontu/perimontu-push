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

        app.Use(ApplyEndpointBodyLimit);

        app.UseRateLimiter();

        app.MapHealthEndpoints();
        app.MapSendEndpoints();
        app.MapReceiptEndpoints();

        return app;
    }

    /// <summary>Los endpoints que declaran <see cref="BodyLimitMetadata"/> (p. ej. el público de lecturas) aceptan cuerpos más pequeños.</summary>
    private static Task ApplyEndpointBodyLimit(HttpContext context, RequestDelegate next)
    {
        var limit = context.GetEndpoint()?.Metadata.GetMetadata<BodyLimitMetadata>();
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (limit is not null && feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = limit.MaxBytes;
        }

        return next(context);
    }

    /// <summary>
    /// Respuesta genérica: nunca se exponen detalles de la excepción. Única excepción: Firebase sin credencial,
    /// que se informa como 503 accionable (el detalle, con el proyecto, queda en el log).
    /// </summary>
    private static Task WriteServerError(HttpContext context)
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (error is FirebaseUnavailableException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Firebase no disponible para este proyecto: revise la credencial")
                .ExecuteAsync(context);
        }

        return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Error interno")
            .ExecuteAsync(context);
    }
}
