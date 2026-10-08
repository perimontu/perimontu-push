using System.ComponentModel.DataAnnotations;

namespace CoberPush.Api.Options;

/// <summary>Rate limiting configurable por política.</summary>
public sealed class RateLimitOptions
{
    public const string SECTION = "RateLimiting";

    public RateLimitPolicyOptions Send { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    public RateLimitPolicyOptions Receipts { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };
}

public sealed class RateLimitPolicyOptions
{
    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 60;

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; } = 60;
}
