using System.Net;
using System.Net.Http.Json;
using CoberPush.Api.Models;
using CoberPush.Api.Services;

namespace CoberPush.Api.Tests.Integration;

public class ReceiptEndpointsTests : IDisposable
{
    private const string DEVICE_TOKEN = "fcm-token-del-dispositivo-ab12";

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>Envía un push real (con sender falso) y devuelve lo que la app recibiría en <c>data</c>.</summary>
    private async Task<PushContent> SendPushAsync()
    {
        var body = new { tokens = new[] { DEVICE_TOKEN }, title = "t", body = "b", messageId = "12345" };
        var response = await _factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", body);
        response.EnsureSuccessStatusCode();
        return _factory.Sender.Sent.Last();
    }

    private static object ReadBody(PushContent content, string? receipt = null, string? deviceToken = DEVICE_TOKEN) => new
    {
        messageId = content.MessageId,
        sentAt = content.SentAt,
        receipt = receipt ?? content.Receipt,
        deviceToken,
        readAt = "2026-10-08T12:30:00Z"
    };

    /// <summary>Cliente de una app instalada: IP cualquiera, sin API key.</summary>
    private HttpClient AppClient(ApiFactory? factory = null) =>
        (factory ?? _factory).CreateClientFrom(ip: "181.10.20.30", apiKey: null);

    [Fact]
    public async Task Lectura_valida_se_reenvia_al_backend_con_el_contrato_documentado()
    {
        var content = await SendPushAsync();
        _factory.Clock.Advance(TimeSpan.FromHours(1)); // el usuario lee 1 h después del envío

        var response = await AppClient().PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var forwarded = Assert.Single(_factory.Forwarder.Received);
        Assert.Equal("read", forwarded.Event);
        Assert.Equal("cober", forwarded.ProjectId);
        Assert.Equal("12345", forwarded.MessageId);
        Assert.Equal(DEVICE_TOKEN, forwarded.DeviceToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(content.SentAt), forwarded.SentAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-08T12:30:00Z"), forwarded.ReadAt);
    }

    [Fact]
    public async Task El_endpoint_de_lectura_no_exige_ip_permitida_ni_api_key()
    {
        var content = await SendPushAsync();

        var response = await _factory.CreateClientFrom(ip: null, apiKey: null)
            .PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Recibo_falso_es_401_y_no_llega_al_backend()
    {
        var content = await SendPushAsync();

        var response = await AppClient().PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content, receipt: "firma-falsa"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Forwarder.Received);
    }

    [Fact]
    public async Task Recibo_de_otro_mensaje_es_401()
    {
        var content = await SendPushAsync();
        var body = new { messageId = "OTRO-ID", sentAt = content.SentAt, receipt = content.Receipt, deviceToken = DEVICE_TOKEN };

        var response = await AppClient().PostAsJsonAsync("/api/cober/receipts/read", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mensaje_de_mas_de_30_dias_es_410()
    {
        var content = await SendPushAsync();
        _factory.Clock.Advance(TimeSpan.FromDays(31));

        var response = await AppClient().PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Empty(_factory.Forwarder.Received);
    }

    [Fact]
    public async Task Vigencia_es_configurable()
    {
        using var factory = new ApiFactory(new() { ["Projects:0:Receipts:MaxAgeDays"] = "1" });
        var body = new { tokens = new[] { DEVICE_TOKEN }, title = "t", body = "b" };
        await factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", body);
        var content = factory.Sender.Sent.Last();
        factory.Clock.Advance(TimeSpan.FromDays(2));

        var response = await AppClient(factory).PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Backend_caido_responde_503_para_que_la_app_reintente()
    {
        var content = await SendPushAsync();
        _factory.Forwarder.Outcome = ForwardOutcome.Unavailable;

        var response = await AppClient().PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Backend_que_rechaza_responde_502()
    {
        var content = await SendPushAsync();
        _factory.Forwarder.Outcome = ForwardOutcome.Rejected;

        var response = await AppClient().PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Fecha_de_lectura_futura_o_ausente_se_reemplaza_por_la_actual()
    {
        var content = await SendPushAsync();
        var body = new { messageId = content.MessageId, sentAt = content.SentAt, receipt = content.Receipt, deviceToken = DEVICE_TOKEN, readAt = "2099-01-01T00:00:00Z" };

        await AppClient().PostAsJsonAsync("/api/cober/receipts/read", body);

        Assert.Equal(_factory.Clock.GetUtcNow(), _factory.Forwarder.Received.Single().ReadAt);
    }

    [Theory]
    [InlineData("""{"messageId":"","sentAt":1,"receipt":"abc","deviceToken":"t"}""")]
    [InlineData("""{"messageId":"a b","sentAt":1,"receipt":"abc","deviceToken":"t"}""")]
    [InlineData("""{"messageId":"1","sentAt":0,"receipt":"abc","deviceToken":"t"}""")]
    [InlineData("""{"messageId":"1","sentAt":1,"receipt":"con espacios","deviceToken":"t"}""")]
    [InlineData("""{"messageId":"1","sentAt":1,"receipt":"abc","deviceToken":""}""")]
    [InlineData("""{}""")]
    public async Task Formato_invalido_es_400(string json)
    {
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await AppClient().PostAsync("/api/cober/receipts/read", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Forwarder.Received);
    }

    [Fact]
    public async Task Token_de_dispositivo_desmesurado_se_rechaza()
    {
        var content = await SendPushAsync();
        var body = new { messageId = content.MessageId, sentAt = content.SentAt, receipt = content.Receipt, deviceToken = new string('x', 50_000) };

        var response = await AppClient().PostAsJsonAsync("/api/cober/receipts/read", body);

        Assert.NotEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(_factory.Forwarder.Received);
    }

    [Fact]
    public async Task Rate_limit_de_lecturas_se_configura_por_separado_y_responde_429()
    {
        using var factory = new ApiFactory(new()
        {
            ["Projects:0:ReceiptsRateLimit:PermitLimit"] = "2",
            ["Projects:0:ReceiptsRateLimit:WindowSeconds"] = "60"
        });
        var body = new { tokens = new[] { DEVICE_TOKEN }, title = "t", body = "b" };
        await factory.CreateClientFrom().PostAsJsonAsync("/api/cober/push/send", body);
        var content = factory.Sender.Sent.Last();
        var client = AppClient(factory);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/cober/receipts/read", ReadBody(content))).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests], statuses);
    }
}
