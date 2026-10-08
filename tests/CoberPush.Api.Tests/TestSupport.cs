using CoberPush.Api.Options;
using CoberPush.Api.Projects;
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

/// <summary>Proyectos de prueba válidos: se parte de uno completo y cada prueba cambia solo lo que necesita.</summary>
public static class TestProjects
{
    public const string ID = "cober";
    public const string HMAC_SECRET = "test-hmac-secret-0123456789-abcdefghijkl";
    public const string API_KEY = "test-api-key-0123456789-abcdefghijklmnop";

    public static ProjectOptions Options(string id = ID, Action<ProjectOptions>? tweak = null)
    {
        var options = new ProjectOptions
        {
            Id = id,
            Name = "Cober Test",
            Firebase = new FirebaseOptions { ProjectId = "cober-test", CredentialsEnvVar = "COBER_TEST_CREDENTIALS" },
            AllowedIps = ["203.0.113.10"],
            ApiKeys = [new ApiKeyOptions { Name = "backend", Key = API_KEY }],
            Receipts = new ReceiptOptions { HmacSecret = HMAC_SECRET, MaxAgeDays = 30 },
            UrlApi = new UrlApiOptions { BaseUrl = "https://backend.test/api/push", ReadPath = "/read", BearerToken = "bearer-de-prueba" },
            UrlPolicy = new UrlPolicyOptions
            {
                AllowedHosts = ["www.cober.com.ar", "cober.com.ar"],
                CanonicalHost = "www.cober.com.ar",
                PathPrefix = "/app"
            }
        };

        tweak?.Invoke(options);
        return options;
    }

    public static Project Create(string id = ID, Action<ProjectOptions>? tweak = null)
    {
        var all = new ProjectsOptions();
        all.Items.Add(Options(id, tweak));

        Assert.True(new ProjectRegistry(Opt.Of(all)).TryGet(id, out var project));
        return project;
    }
}

/// <summary>Construye los servicios reales con opciones por defecto (sin contenedor DI).</summary>
public static class TestServices
{
    public static HmacReceiptSigner CreateSigner(TimeProvider clock) => new(clock);

    public static UrlPolicy CreateUrlPolicy() => new();

    public static PushRequestValidator CreateValidator(TimeProvider clock, PushOptions? push = null)
    {
        push ??= new PushOptions();
        return new PushRequestValidator(Opt.Of(push), CreateUrlPolicy(), CreateSigner(clock), clock);
    }
}
