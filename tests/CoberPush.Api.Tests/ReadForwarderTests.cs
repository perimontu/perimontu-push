using System.Net;
using CoberPush.Api.Models;
using CoberPush.Api.Options;
using CoberPush.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Tests;

public class PhpReadForwarderTests
{
    private static readonly PhpReadPayload PAYLOAD = new(
        "read", "12345", "token-largo-ab12", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1));

    private static PhpReadForwarder Create(StubHandler handler, int retryCount = 2, string readPath = "/read")
    {
        var options = Opt.Of(new PhpApiOptions
        {
            BaseUrl = "https://php.test/api/push/",
            ReadPath = readPath,
            BearerToken = "secreto",
            RetryCount = retryCount
        });

        return new PhpReadForwarder(new HttpClient(handler), options, NullLogger<PhpReadForwarder>.Instance);
    }

    [Fact]
    public async Task Respuesta_2xx_se_acepta_y_arma_la_url_desde_la_configuracion()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        var outcome = await Create(handler, readPath: "leido").ForwardReadAsync(PAYLOAD);

        Assert.Equal(ForwardOutcome.Accepted, outcome);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("https://php.test/api/push/leido", handler.LastUri?.ToString());
    }

    [Fact]
    public async Task El_cuerpo_enviado_a_php_tiene_el_contrato_documentado()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        await Create(handler).ForwardReadAsync(PAYLOAD);

        Assert.Contains("\"event\":\"read\"", handler.LastBody);
        Assert.Contains("\"messageId\":\"12345\"", handler.LastBody);
        Assert.Contains("\"deviceToken\":\"token-largo-ab12\"", handler.LastBody);
        Assert.Contains("\"sentAt\":", handler.LastBody);
        Assert.Contains("\"readAt\":", handler.LastBody);
    }

    [Fact]
    public async Task Error_4xx_se_rechaza_sin_reintentar()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest);

        var outcome = await Create(handler).ForwardReadAsync(PAYLOAD);

        Assert.Equal(ForwardOutcome.Rejected, outcome);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Error_5xx_reintenta_y_si_se_recupera_se_acepta()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway, HttpStatusCode.OK);

        var outcome = await Create(handler, retryCount: 2).ForwardReadAsync(PAYLOAD);

        Assert.Equal(ForwardOutcome.Accepted, outcome);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Error_5xx_persistente_agota_los_reintentos_configurados()
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable);

        var outcome = await Create(handler, retryCount: 1).ForwardReadAsync(PAYLOAD);

        Assert.Equal(ForwardOutcome.Unavailable, outcome);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Sin_reintentos_configurados_hace_un_solo_intento()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError);

        var outcome = await Create(handler, retryCount: 0).ForwardReadAsync(PAYLOAD);

        Assert.Equal(ForwardOutcome.Unavailable, outcome);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Falla_de_red_es_no_disponible()
    {
        var handler = new StubHandler { Throw = new HttpRequestException("sin conexión") };

        var outcome = await Create(handler, retryCount: 0).ForwardReadAsync(PAYLOAD);

        Assert.Equal(ForwardOutcome.Unavailable, outcome);
    }

    [Fact]
    public async Task Timeout_es_no_disponible()
    {
        var handler = new StubHandler { Throw = new TaskCanceledException("timeout") };

        var outcome = await Create(handler, retryCount: 0).ForwardReadAsync(PAYLOAD);

        Assert.Equal(ForwardOutcome.Unavailable, outcome);
    }

    [Fact]
    public async Task Cancelacion_del_cliente_se_propaga()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new StubHandler(HttpStatusCode.OK);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Create(handler).ForwardReadAsync(PAYLOAD, cts.Token));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;
        private readonly HttpStatusCode _last;

        public StubHandler(params HttpStatusCode[] statuses)
        {
            _statuses = new Queue<HttpStatusCode>(statuses);
            _last = statuses.Length > 0 ? statuses[^1] : HttpStatusCode.OK;
        }

        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }
        public string LastBody { get; private set; } = string.Empty;
        public Exception? Throw { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            LastUri = request.RequestUri;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);

            if (Throw is not null)
            {
                throw Throw;
            }

            var status = _statuses.Count > 0 ? _statuses.Dequeue() : _last;
            return new HttpResponseMessage(status);
        }
    }
}
