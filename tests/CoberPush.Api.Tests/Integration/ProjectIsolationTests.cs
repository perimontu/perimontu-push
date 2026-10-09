using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoberPush.Api.Models;

namespace CoberPush.Api.Tests.Integration;

/// <summary>Aislamiento entre proyectos y reglas por consumidor (API key): quién puede qué, desde dónde y con qué cuota.</summary>
public class ProjectIsolationTests : IDisposable
{
    private const string DEVICE_TOKEN = "fcm-token-del-dispositivo-ab12";

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static object Body() => new { tokens = new[] { "token-aaaa-1111" }, title = "t", body = "b" };

    private static object ReadBody(PushContent content) =>
        new { messageId = content.MessageId, sentAt = content.SentAt, receipt = content.Receipt, deviceToken = DEVICE_TOKEN };

    private static async Task<string> ErrorBody(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    // --- Autenticación por proyecto -------------------------------------------------------------------------

    [Fact]
    public async Task La_key_de_un_proyecto_no_sirve_en_otro()
    {
        var onOther = await _factory.CreateClientFrom(apiKey: ApiFactory.API_KEY)
            .PostAsJsonAsync("/api/otro/push/send", Body());
        var onOwn = await _factory.CreateClientFrom(apiKey: ApiFactory.OTHER_KEY)
            .PostAsJsonAsync("/api/cober/push/send", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, onOther.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, onOwn.StatusCode);
        Assert.Empty(_factory.Sender.Sent);
    }

    [Fact]
    public async Task Cada_proyecto_envia_con_su_propio_contexto()
    {
        await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", Body());
        var response = await _factory.CreateClientFrom(apiKey: ApiFactory.OTHER_KEY).PostAsJsonAsync("/api/otro/push/send", Body());

        Assert.Equal(["cober", "otro"], _factory.Sender.SentProjects);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("otro", json.GetProperty("projectId").GetString());
    }

    [Fact]
    public async Task Proyecto_inexistente_responde_igual_que_una_key_incorrecta()
    {
        var unknown = await _factory.CreateClientFrom().PostAsJsonAsync("/api/noexiste/push/send", Body());
        var wrongKey = await _factory.CreateClientFrom(apiKey: "otra-clave").PostAsJsonAsync("/api/cober/push/send", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(wrongKey.StatusCode, unknown.StatusCode);
        Assert.Equal(await ErrorBody(wrongKey), await ErrorBody(unknown));
    }

    [Fact]
    public async Task El_id_de_proyecto_no_distingue_mayusculas()
    {
        var response = await _factory.CreateClientFrom().PostAsJsonAsync("/api/COBER/push/send", Body());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("cober", Assert.Single(_factory.Sender.SentProjects));
    }

    [Theory]
    [InlineData(ApiFactory.DISABLED_KEY)]
    [InlineData(ApiFactory.EXPIRED_KEY)]
    public async Task Key_deshabilitada_o_vencida_es_401(string key)
    {
        var response = await _factory.CreateClientFrom(apiKey: key).PostAsJsonAsync("/api/cober/push/send", Body());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Sender.Sent);
    }

    [Fact]
    public async Task Key_con_vencimiento_deja_de_valer_a_la_hora_indicada()
    {
        var factory = new ApiFactory(new() { ["Projects:0:ApiKeys:0:ExpiresAt"] = "2026-10-08T13:00:00-03:00" });
        using var _ = factory;
        var client = factory.CreateClientFrom();

        var before = await client.PostAsJsonAsync("/api/cober/push/send", Body());
        factory.Clock.Advance(TimeSpan.FromHours(4)); // 12:00Z + 4 h = 16:00Z, pasadas las 16:00Z (13:00-03:00)
        var after = await client.PostAsJsonAsync("/api/cober/push/send", Body());

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Proyecto_deshabilitado_es_403_con_key_valida_y_404_en_lecturas()
    {
        var send = await _factory.CreateClientFrom(apiKey: ApiFactory.DISABLED_PROJECT_KEY)
            .PostAsJsonAsync("/api/pausado/push/send", Body());
        var read = await _factory.CreateClientFrom(ip: "181.10.20.30", apiKey: null)
            .PostAsJsonAsync("/api/pausado/receipts/read", new { messageId = "1", sentAt = 1, receipt = "abc", deviceToken = "t" });

        Assert.Equal(HttpStatusCode.Forbidden, send.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Empty(_factory.Sender.Sent);
    }

    // --- IPs: nivel proyecto y sobrescritura por key --------------------------------------------------------

    [Fact]
    public async Task Key_con_ips_propias_sobrescribe_las_del_proyecto()
    {
        var fromOwn = await _factory.CreateClientFrom(ip: ApiFactory.RESTRICTED_IP, apiKey: ApiFactory.RESTRICTED_KEY)
            .PostAsJsonAsync("/api/cober/push/send", Body());
        var fromProjectIp = await _factory.CreateClientFrom(ip: ApiFactory.ALLOWED_IP, apiKey: ApiFactory.RESTRICTED_KEY)
            .PostAsJsonAsync("/api/cober/push/send", Body());

        Assert.Equal(HttpStatusCode.OK, fromOwn.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, fromProjectIp.StatusCode);
    }

    [Fact]
    public async Task Key_sin_ips_propias_usa_las_del_proyecto()
    {
        var fromProjectIp = await _factory.CreateClientFrom(ip: ApiFactory.ALLOWED_IP).PostAsJsonAsync("/api/cober/push/send", Body());
        var fromKeyOnlyIp = await _factory.CreateClientFrom(ip: ApiFactory.RESTRICTED_IP).PostAsJsonAsync("/api/cober/push/send", Body());

        Assert.Equal(HttpStatusCode.OK, fromProjectIp.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, fromKeyOnlyIp.StatusCode);
    }

    // --- Rate limit por key ---------------------------------------------------------------------------------

    [Fact]
    public async Task El_limite_de_una_key_no_afecta_a_las_demas()
    {
        var limited = _factory.CreateClientFrom(apiKey: ApiFactory.LIMITED_KEY);
        for (var i = 0; i < 3; i++)
        {
            await limited.PostAsJsonAsync("/api/cober/push/send", Body());
        }

        var otherKey = await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", Body());

        Assert.Equal(HttpStatusCode.OK, otherKey.StatusCode);
    }

    [Fact]
    public async Task Sin_key_valida_se_aplica_un_limite_estricto_por_ip()
    {
        using var factory = new ApiFactory(new()
        {
            ["Api:UnauthenticatedRateLimit:PermitLimit"] = "2",
            ["Api:UnauthenticatedRateLimit:WindowSeconds"] = "60"
        });
        var client = factory.CreateClientFrom(apiKey: "adivinando");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/cober/push/send", Body())).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    // --- Lecturas por proyecto ------------------------------------------------------------------------------

    [Fact]
    public async Task Un_recibo_firmado_en_un_proyecto_no_sirve_en_otro()
    {
        await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", Body());
        var content = _factory.Sender.Sent.Last();
        var app = _factory.CreateClientFrom(ip: "181.10.20.30", apiKey: null);

        var inOther = await app.PostAsJsonAsync("/api/otro/receipts/read", ReadBody(content));
        var inOwn = await app.PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content));

        Assert.Equal(HttpStatusCode.Unauthorized, inOther.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, inOwn.StatusCode);
    }

    [Fact]
    public async Task Cada_proyecto_reenvia_la_lectura_a_su_backend()
    {
        await _factory.CreateClientFrom(apiKey: ApiFactory.OTHER_KEY).PostAsJsonAsync("/api/otro/push/send", Body());
        var content = _factory.Sender.Sent.Last();

        await _factory.CreateClientFrom(ip: "181.10.20.30", apiKey: null)
            .PostAsJsonAsync("/api/otro/receipts/read", ReadBody(content));

        Assert.Equal("otro", Assert.Single(_factory.Forwarder.ReceivedProjects));
        Assert.Equal("otro", _factory.Forwarder.Received.Single().ProjectId);
    }

    [Fact]
    public async Task Lectura_de_un_proyecto_inexistente_es_404()
    {
        var response = await _factory.CreateClientFrom(ip: "181.10.20.30", apiKey: null)
            .PostAsJsonAsync("/api/noexiste/receipts/read", new { messageId = "1", sentAt = 1, receipt = "abc", deviceToken = "t" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
