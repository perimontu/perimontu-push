namespace CoberPush.Api.Services;

/// <summary>Regla de URLs permitidas al tocar la notificación (equivalente a <c>AppLinks.TryNormalize</c> de CoberApp).</summary>
public interface IUrlPolicy
{
    /// <summary>Valida <paramref name="url"/> y la normaliza al host canónico. Evita abrir sitios ajenos dentro del WebView.</summary>
    bool TryNormalize(string? url, out string? normalized);
}
