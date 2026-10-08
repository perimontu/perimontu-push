using CoberPush.Api.Services;

namespace CoberPush.Api.Tests;

public class UrlPolicyTests
{
    private readonly UrlPolicy _policy = TestServices.CreateUrlPolicy();
    private readonly CoberPush.Api.Projects.Project _project = TestProjects.Create();

    [Theory]
    [InlineData("https://cober.com.ar/app/pwa/turnos", "https://www.cober.com.ar/app/pwa/turnos")]
    [InlineData("https://www.cober.com.ar/app", "https://www.cober.com.ar/app")]
    [InlineData("https://WWW.COBER.COM.AR/app/x?y=1", "https://www.cober.com.ar/app/x?y=1")]
    [InlineData("https://cober.com.ar:443/app/", "https://www.cober.com.ar/app/")]
    public void Acepta_y_normaliza_al_host_canonico(string input, string expected)
    {
        Assert.True(_policy.TryNormalize(_project, input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://www.cober.com.ar/app")]
    [InlineData("https://evil.com/app")]
    [InlineData("https://www.cober.com.ar.evil.com/app")]
    [InlineData("https://www.cober.com.ar/")]
    [InlineData("https://www.cober.com.ar/application")]
    [InlineData("https://www.cober.com.ar/otra/app")]
    [InlineData("https://user:pass@www.cober.com.ar/app")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/app/relativa")]
    public void Rechaza_urls_fuera_de_la_politica(string? input)
    {
        Assert.False(_policy.TryNormalize(_project, input, out var normalized));
        Assert.Null(normalized);
    }
}
