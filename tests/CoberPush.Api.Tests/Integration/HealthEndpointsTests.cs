using System.Net;
using System.Text.Json;
using CoberPush.Api.Services;

namespace CoberPush.Api.Tests.Integration;

/// <summary>Health por proyecto: exige key y comprueba Firebase (con caché de 60 s).</summary>
public class HealthEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Health_simple_sigue_publico()
    {
        var response = await _factory.CreateClientFrom(ip: "8.8.8.8", apiKey: null).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_de_proyecto_sin_key_es_401()
    {
        var response = await _factory.CreateClientFrom(apiKey: null).GetAsync("/api/cober/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, _factory.Sender.ConnectivityCalls);
    }

    [Fact]
    public async Task Health_con_la_key_de_otro_proyecto_o_proyecto_inexistente_es_401()
    {
        var wrongProject = await _factory.CreateClientFrom(apiKey: ApiFactory.OTHER_KEY).GetAsync("/api/cober/health");
        var unknown = await _factory.CreateClientFrom().GetAsync("/api/noexiste/health");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongProject.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Health_correcto_informa_el_proyecto_y_firebase()
    {
        var response = await _factory.CreateClientFrom().GetAsync("/api/cober/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("cober", json.GetProperty("projectId").GetString());
        Assert.Equal("ok", json.GetProperty("status").GetString());
        Assert.Equal("ok", json.GetProperty("firebase").GetString());
        Assert.True(json.TryGetProperty("checkedAt", out _));
    }

    [Fact]
    public async Task Health_no_exige_ip_permitida_para_enviar()
    {
        var response = await _factory.CreateClientFrom(ip: "8.8.8.8").GetAsync("/api/cober/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Firebase_con_error_responde_503_sin_detalles()
    {
        _factory.Sender.ConnectivityResult = false;

        var response = await _factory.CreateClientFrom().GetAsync("/api/cober/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("error", json.GetProperty("firebase").GetString());
    }

    [Fact]
    public async Task Credencial_ausente_se_informa_como_error_de_firebase_y_no_como_500()
    {
        _factory.Sender.ConnectivityFailure = new FirebaseUnavailableException("detalle-interno-secreto");

        var response = await _factory.CreateClientFrom().GetAsync("/api/cober/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("detalle-interno-secreto", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_resultado_se_cachea_60_segundos_por_proyecto()
    {
        var client = _factory.CreateClientFrom();

        await client.GetAsync("/api/cober/health");
        await client.GetAsync("/api/cober/health");
        Assert.Equal(1, _factory.Sender.ConnectivityCalls);

        await _factory.CreateClientFrom(apiKey: ApiFactory.OTHER_KEY).GetAsync("/api/otro/health");
        Assert.Equal(2, _factory.Sender.ConnectivityCalls);

        _factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await client.GetAsync("/api/cober/health");
        Assert.Equal(3, _factory.Sender.ConnectivityCalls);
    }
}
