using CoberPush.Api.Options;
using CoberPush.Api.Services;
using CoberPush.Api.Validation;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Tests;

/// <summary>Evita la ambigüedad entre el namespace CoberPush.Api.Options y la clase Options del framework.</summary>
public static class Opt
{
    public static IOptions<T> Of<T>(T value) where T : class => Microsoft.Extensions.Options.Options.Create(value);
}

/// <summary>Reloj controlable para probar vigencias.</summary>
public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset? start = null)
    {
        _now = start ?? new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Construye los servicios reales con opciones por defecto (sin contenedor DI).</summary>
public static class TestServices
{
    public const string HMAC_SECRET = "test-hmac-secret-0123456789-abcdefghijkl";

    public static HmacReceiptSigner CreateSigner(TimeProvider clock, string secret = HMAC_SECRET, int maxAgeDays = 30)
    {
        var options = Opt.Of(new ReceiptOptions { HmacSecret = secret, MaxAgeDays = maxAgeDays });
        return new HmacReceiptSigner(options, clock);
    }

    public static UrlPolicy CreateUrlPolicy(PushOptions? push = null)
    {
        return new UrlPolicy(Opt.Of(push ?? new PushOptions()));
    }

    public static PushRequestValidator CreateValidator(TimeProvider clock, PushOptions? push = null)
    {
        push ??= new PushOptions();
        return new PushRequestValidator(
            Opt.Of(push), CreateUrlPolicy(push), CreateSigner(clock), clock);
    }
}
