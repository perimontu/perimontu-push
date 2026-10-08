namespace CoberPush.Api.Endpoints;

/// <summary>Nombres de las políticas de rate limiting (se configuran en <c>RateLimiting</c> de appsettings).</summary>
public static class RateLimitPolicies
{
    public const string SEND = "send";
    public const string RECEIPTS = "receipts";
}
