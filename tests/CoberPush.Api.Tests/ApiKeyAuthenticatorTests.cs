using CoberPush.Api.Options;
using CoberPush.Api.Projects;
using CoberPush.Api.Security;
using Microsoft.AspNetCore.Http;

namespace CoberPush.Api.Tests;

public class ApiKeyAuthenticatorTests
{
    private readonly FakeClock _clock = new();
    private readonly ApiKeyAuthenticator _authenticator;

    public ApiKeyAuthenticatorTests()
    {
        var all = new ProjectsOptions();
        all.Items.Add(TestProjects.Options("cober", o =>
        {
            o.ApiKeys[0].ExpiresAt = _clock.GetUtcNow().AddDays(1);
            o.ApiKeys.Add(new ApiKeyOptions { Name = "apagada", Key = "apagada-key-0123456789-abcdefghijklmnopqrs", Enabled = false });
        }));
        all.Items.Add(TestProjects.Options("otro", o => o.ApiKeys[0].Key = "otro-key-0123456789-abcdefghijklmnopqrstuv"));

        _authenticator = new ApiKeyAuthenticator(new ProjectRegistry(Opt.Of(all)), Opt.Of(new ApiOptions()), _clock);
    }

    private static DefaultHttpContext Request(string? projectId, string? key)
    {
        var context = new DefaultHttpContext();

        if (projectId is not null)
        {
            context.Request.RouteValues[ApiKeyAuthenticator.ROUTE_PARAMETER] = projectId;
        }

        if (key is not null)
        {
            context.Request.Headers["X-Api-Key"] = key;
        }

        return context;
    }

    [Fact]
    public void Key_valida_del_proyecto_se_autentica()
    {
        var result = _authenticator.Authenticate(Request("cober", TestProjects.API_KEY));

        Assert.Equal("cober", result.Project?.Id);
        Assert.Equal("backend", result.Key?.Name);
    }

    [Fact]
    public void Key_de_otro_proyecto_no_se_autentica_pero_el_proyecto_se_resuelve()
    {
        var result = _authenticator.Authenticate(Request("cober", "otro-key-0123456789-abcdefghijklmnopqrstuv"));

        Assert.NotNull(result.Project);
        Assert.Null(result.Key);
    }

    [Theory]
    [InlineData("cober", null)]
    [InlineData("cober", "")]
    [InlineData("cober", "incorrecta")]
    [InlineData("noexiste", TestProjects.API_KEY)]
    [InlineData(null, TestProjects.API_KEY)]
    public void Sin_key_valida_no_hay_consumidor(string? projectId, string? key)
    {
        Assert.Null(_authenticator.Authenticate(Request(projectId, key)).Key);
    }

    [Fact]
    public void Key_deshabilitada_no_se_autentica()
    {
        var result = _authenticator.Authenticate(Request("cober", "apagada-key-0123456789-abcdefghijklmnopqrs"));

        Assert.Null(result.Key);
    }

    [Fact]
    public void Key_vencida_no_se_autentica()
    {
        _clock.Advance(TimeSpan.FromDays(2));

        Assert.Null(_authenticator.Authenticate(Request("cober", TestProjects.API_KEY)).Key);
    }

    [Fact]
    public void La_cabecera_de_la_key_es_configurable()
    {
        var all = new ProjectsOptions();
        all.Items.Add(TestProjects.Options());
        var custom = new ApiKeyAuthenticator(
            new ProjectRegistry(Opt.Of(all)), Opt.Of(new ApiOptions { KeyHeader = "X-Mi-Clave" }), _clock);
        var context = Request("cober", null);
        context.Request.Headers["X-Mi-Clave"] = TestProjects.API_KEY;

        Assert.NotNull(custom.Authenticate(context).Key);
    }
}
