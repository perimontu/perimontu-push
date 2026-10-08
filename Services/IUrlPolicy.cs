using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

/// <summary>Regla de URLs permitidas al tocar la notificación (equivalente a <c>AppLinks.TryNormalize</c> de CoberApp).</summary>
public interface IUrlPolicy
{
    /// <summary>Valida <paramref name="url"/> contra la regla del proyecto y la normaliza a su host canónico.</summary>
    bool TryNormalize(Project project, string? url, out string? normalized);
}
