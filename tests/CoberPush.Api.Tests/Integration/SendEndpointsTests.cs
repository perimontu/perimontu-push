using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace CoberPush.Api.Tests.Integration;

public class SendEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static object ValidBody(params string[] tokens) => new
    {
        tokens = tokens.Length > 0 ? tokens : ["token-aaaa-1111"],
        title = "Turno confirmado",
        body = "Tu turno es mañana a las 10:00",
        url = "https://cober.com.ar/app/pwa/central_de_turnos",
        messageId = "12345",
        data = new { tipo = "turno" }
    };

    [Fact]
    public async Task Health_no_requiere_autenticacion_ni_ip()
    {
        var client = _factory.CreateClientFrom(ip: "8.8.8.8", apiKey: null);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Envio_valido_devuelve_resultados_con_token_enmascarado()
    {
        var response = await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", ValidBody("token-aaaa-1111"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("cober", json.GetProperty("projectId").GetString());
        Assert.Equal("12345", json.GetProperty("messageId").GetString());
        Assert.Equal(1, json.GetProperty("successCount").GetInt32());
        Assert.Equal(0, json.GetProperty("failureCount").GetInt32());
        var result = json.GetProperty("results")[0];
        Assert.Equal("…1111", result.GetProperty("target").GetString());
        Assert.False(result.GetProperty("removeToken").GetBoolean());
    }

    [Fact]
    public async Task Envio_normaliza_url_y_firma_el_contenido()
    {
        await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", ValidBody());

        var sent = Assert.Single(_factory.Sender.Sent);
        Assert.Equal("https://www.cober.com.ar/app/pwa/central_de_turnos", sent.Url);
        Assert.Equal("12345", sent.MessageId);
        Assert.False(string.IsNullOrEmpty(sent.Receipt));
    }

    [Fact]
    public async Task Token_desregistrado_se_marca_para_borrar()
    {
        var response = await _factory.CreateClientFrom()
            .PostAsJsonAsync("/api/cober/push/send", ValidBody("token-aaaa-1111", FakePushSender.UNREGISTERED_TOKEN));

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, json.GetProperty("successCount").GetInt32());
        Assert.Equal(1, json.GetProperty("failureCount").GetInt32());
        var failed = json.GetProperty("results")[1];
        Assert.Equal("UNREGISTERED", failed.GetProperty("errorCode").GetString());
        Assert.True(failed.GetProperty("removeToken").GetBoolean());
    }

    [Fact]
    public async Task Tokens_repetidos_se_envian_una_sola_vez()
    {
        var response = await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", ValidBody("dup-token-1", "dup-token-1"));

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, json.GetProperty("results").GetArrayLength());
    }

    [Fact]
    public async Task DryRun_se_propaga_al_sender()
    {
        var body = new { tokens = new[] { "token-aaaa-1111" }, title = "t", body = "b", dryRun = true };

        await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", body);

        Assert.True(_factory.Sender.LastDryRun);
    }

    [Fact]
    public async Task Sin_api_key_o_con_clave_incorrecta_es_401()
    {
        var noKey = await _factory.CreateClientFrom(apiKey: null).PostAsJsonAsync("/api/cober/push/send", ValidBody());
        var wrongKey = await _factory.CreateClientFrom(apiKey: "otra-clave").PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
        Assert.Empty(_factory.Sender.Sent);
    }

    [Fact]
    public async Task Ip_no_permitida_es_403_aunque_la_clave_sea_correcta()
    {
        var response = await _factory.CreateClientFrom(ip: "8.8.8.8").PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Sender.Sent);
    }

    [Fact]
    public async Task Sin_ip_remota_no_se_permite()
    {
        var response = await _factory.CreateClientFrom(ip: null).PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Rango_cidr_configurado_se_permite()
    {
        var response = await _factory.CreateClientFrom(ip: "198.51.100.200").PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Detras_del_proxy_de_confianza_se_usa_la_ip_de_x_forwarded_for()
    {
        var client = _factory.CreateClientFrom(ip: "127.0.0.1");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ApiFactory.ALLOWED_IP);

        var response = await client.PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task X_forwarded_for_de_un_cliente_que_no_es_proxy_se_ignora()
    {
        var client = _factory.CreateClientFrom(ip: "8.8.8.8");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ApiFactory.ALLOWED_IP);

        var response = await client.PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{"tokens":[],"title":"t","body":"b"}""")]
    [InlineData("""{"tokens":["a"],"title":"","body":"b"}""")]
    [InlineData("""{"tokens":["a"],"title":"t","body":"b","url":"https://evil.com/app"}""")]
    [InlineData("""{"tokens":["a"],"title":"t","body":"b","data":{"url":"https://evil.com"}}""")]
    [InlineData("""{"tokens":["a"],"title":"t","body":"b","ttlSeconds":-5}""")]
    public async Task Pedidos_invalidos_son_400_y_no_se_envian(string json)
    {
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _factory.CreateClientFrom().PostAsync("/api/cober/push/send", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Sender.Sent);
    }

    [Fact]
    public async Task Mas_tokens_que_el_maximo_configurado_es_400()
    {
        using var factory = new ApiFactory(new() { ["Push:MaxTokensPerRequest"] = "2" });
        var tokens = new[] { "tok-aaa-1", "tok-aaa-2", "tok-aaa-3" };

        var response = await factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", ValidBody(tokens));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Campos_desmesurados_se_rechazan_antes_de_enviar()
    {
        using var factory = new ApiFactory(new() { ["Push:MaxBodyBytes"] = "1024" });
        var huge = new { tokens = new[] { "a" }, title = "t", body = new string('x', 5000) };

        var response = await factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", huge);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.Sender.Sent);
    }

    [Fact]
    public async Task Envio_a_topic_funciona_y_valida_el_nombre()
    {
        var client = _factory.CreateClientFrom();
        var body = new { title = "t", body = "b" };

        var ok = await client.PostAsJsonAsync("/api/cober/push/topic/novedades", body);
        var bad = await client.PostAsJsonAsync("/api/cober/push/topic/con%20espacio", body);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("novedades", _factory.Sender.LastTopic);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Topic_tambien_exige_ip_y_clave()
    {
        var body = new { title = "t", body = "b" };

        var noKey = await _factory.CreateClientFrom(apiKey: null).PostAsJsonAsync("/api/cober/push/topic/x", body);
        var badIp = await _factory.CreateClientFrom(ip: "8.8.8.8").PostAsJsonAsync("/api/cober/push/topic/x", body);

        Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, badIp.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_de_la_key_es_propio_y_responde_429()
    {
        var limited = _factory.CreateClientFrom(apiKey: ApiFactory.LIMITED_KEY);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await limited.PostAsJsonAsync("/api/cober/push/send", ValidBody())).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Error_interno_no_expone_detalles()
    {
        using var throwing = new ApiFactory(
            configureServices: s => s.AddSingleton<CoberPush.Api.Services.IPushSender, ThrowingSender>());
        var response = await throwing.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secreto-interno", text);
    }

    [Fact]
    public async Task Firebase_sin_credencial_responde_503_accionable_sin_filtrar_detalles()
    {
        using var factory = new ApiFactory(
            configureServices: s => s.AddSingleton<CoberPush.Api.Services.IPushSender, FirebaseDownSender>());

        var response = await factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", ValidBody());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Firebase no disponible", text);
        Assert.DoesNotContain("ruta-secreta", text);
    }

    private sealed class FirebaseDownSender : CoberPush.Api.Services.IPushSender
    {
        private static CoberPush.Api.Services.FirebaseUnavailableException Failure() =>
            new("x", new InvalidOperationException("ruta-secreta"));

        public Task<CoberPush.Api.Models.PushSendResult> SendToTokensAsync(
            CoberPush.Api.Projects.Project project, CoberPush.Api.Models.PushContent content, IReadOnlyList<string> tokens, bool dryRun, CancellationToken ct = default)
            => throw Failure();

        public Task<CoberPush.Api.Models.PushSendResult> SendToTopicAsync(
            CoberPush.Api.Projects.Project project, CoberPush.Api.Models.PushContent content, string topic, bool dryRun, CancellationToken ct = default)
            => throw Failure();

        public Task<bool> CheckConnectivityAsync(CoberPush.Api.Projects.Project project, CancellationToken ct = default)
            => throw Failure();
    }

    private sealed class ThrowingSender : CoberPush.Api.Services.IPushSender
    {
        public Task<CoberPush.Api.Models.PushSendResult> SendToTokensAsync(
            CoberPush.Api.Projects.Project project, CoberPush.Api.Models.PushContent content, IReadOnlyList<string> tokens, bool dryRun, CancellationToken ct = default)
            => throw new InvalidOperationException("secreto-interno");

        public Task<CoberPush.Api.Models.PushSendResult> SendToTopicAsync(
            CoberPush.Api.Projects.Project project, CoberPush.Api.Models.PushContent content, string topic, bool dryRun, CancellationToken ct = default)
            => throw new InvalidOperationException("secreto-interno");

        public Task<bool> CheckConnectivityAsync(CoberPush.Api.Projects.Project project, CancellationToken ct = default)
            => throw new InvalidOperationException("secreto-interno");
    }
}
