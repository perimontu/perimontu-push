namespace CoberPush.Api.Endpoints;

/// <summary>Nombres de las políticas de rate limiting (se resuelven por API key o por proyecto, ver <c>RateLimitPartitioner</c>).</summary>
public static class RateLimitPolicies
{
    public const string SEND = "send";
    public const string RECEIPTS = "receipts";
    public const string HEALTH = "health";
}
