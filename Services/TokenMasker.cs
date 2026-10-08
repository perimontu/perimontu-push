namespace CoberPush.Api.Services;

/// <summary>Enmascara tokens FCM para respuestas y logs (solo los últimos caracteres).</summary>
public static class TokenMasker
{
    private const int VISIBLE_CHARS = 4;

    public static string Mask(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length <= VISIBLE_CHARS)
        {
            return "…";
        }

        return "…" + token[^VISIBLE_CHARS..];
    }
}
