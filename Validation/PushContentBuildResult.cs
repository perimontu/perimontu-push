using CoberPush.Api.Models;

namespace CoberPush.Api.Validation;

/// <summary>Resultado de validar un pedido: contenido listo o errores por campo (formato de <c>ValidationProblem</c>).</summary>
public sealed class PushContentBuildResult
{
    public PushContent? Content { get; init; }

    public Dictionary<string, string[]> Errors { get; init; } = new();

    public bool IsValid => Content is not null && Errors.Count == 0;
}
