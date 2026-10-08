using System.Net;
using CoberPush.Api.Models;
using CoberPush.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CoberPush.Api.Tests.Integration;

/// <summary>Sender falso: no usa Firebase. El token "bad-token-0001" simula un token desregistrado.</summary>
public sealed class FakePushSender : IPushSender
{
    public const string UNREGISTERED_TOKEN = "bad-token-0001";

    public List<PushContent> Sent { get; } = [];
    public bool? LastDryRun { get; private set; }
    public string? LastTopic { get; private set; }

    public Task<PushSendResult> SendToTokensAsync(
        PushContent content, IReadOnlyList<string> tokens, bool dryRun, CancellationToken ct = default)
    {
        Sent.Add(content);
        LastDryRun = dryRun;

        var results = tokens
            .Select(t => t == UNREGISTERED_TOKEN
                ? new PushTargetResult(TokenMasker.Mask(t), false, null, "UNREGISTERED", true)
                : new PushTargetResult(TokenMasker.Mask(t), true, $"projects/p/messages/{Guid.NewGuid():N}", null, false))
            .ToList();

        var ok = results.Count(r => r.Success);
        return Task.FromResult(new PushSendResult(content.MessageId, ok, results.Count - ok, results));
    }

    public Task<PushSendResult> SendToTopicAsync(
        PushContent content, string topic, bool dryRun, CancellationToken ct = default)
    {
        Sent.Add(content);
        LastTopic = topic;
        LastDryRun = dryRun;

        var result = new PushTargetResult($"topic:{topic}", true, "projects/p/messages/1", null, false);
        return Task.FromResult(new PushSendResult(content.MessageId, 1, 0, [result]));
    }
}

public sealed class FakePhpReadForwarder : IPhpReadForwarder
{
    public ForwardOutcome Outcome { get; set; } = ForwardOutcome.Accepted;
    public List<PhpReadPayload> Received { get; } = [];

    public Task<ForwardOutcome> ForwardReadAsync(PhpReadPayload payload, CancellationToken ct = default)
    {
        Received.Add(payload);
        return Task.FromResult(Outcome);
    }
}

/// <summary>
/// API completa en memoria con Firebase y PHP simulados. La IP del cliente se controla con la cabecera
/// <see cref="REMOTE_IP_HEADER"/> (TestServer no tiene IP remota por sí mismo).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string REMOTE_IP_HEADER = "X-Test-Remote-Ip";
    public const string API_KEY = "test-api-key-0123456789-abcdefghijklmnop";
    public const string ALLOWED_IP = "203.0.113.10";

    private readonly Dictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _configureServices;

    public ApiFactory(
        Dictionary<string, string?>? overrides = null,
        bool withApiKey = true,
        Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
        _settings = new Dictionary<string, string?>
        {
            ["Api:AllowedSendIps:0"] = ALLOWED_IP,
            ["Api:AllowedSendIps:1"] = "198.51.100.0/24",
            ["Api:KnownProxies:0"] = "127.0.0.1",
            ["Receipts:HmacSecret"] = TestServices.HMAC_SECRET,
            ["PhpApi:BearerToken"] = "bearer-de-prueba",
            ["PhpApi:BaseUrl"] = "https://php.test/api/push"
        };

        if (withApiKey)
        {
            _settings["Api:Key"] = API_KEY;
        }

        foreach (var (key, value) in overrides ?? [])
        {
            _settings[key] = value;
        }
    }

    public FakePushSender Sender { get; } = new();
    public FakePhpReadForwarder Forwarder { get; } = new();
    public FakeClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development" cargaría los user-secrets de la máquina y haría depender las pruebas del equipo.
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, RemoteIpStartupFilter>();
            services.AddSingleton<IPushSender>(Sender);
            services.AddSingleton<IPhpReadForwarder>(Forwarder);
            services.AddSingleton<TimeProvider>(Clock);
            _configureServices?.Invoke(services);
        });
    }

    public HttpClient CreateClientFrom(string? ip = ALLOWED_IP, string? apiKey = API_KEY)
    {
        var client = CreateClient();

        if (ip is not null)
        {
            client.DefaultRequestHeaders.Add(REMOTE_IP_HEADER, ip);
        }

        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }

    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
            Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue(REMOTE_IP_HEADER, out var value)
                        && IPAddress.TryParse(value.ToString(), out var ip))
                    {
                        context.Connection.RemoteIpAddress = ip;
                    }

                    return nextMiddleware(context);
                });

                next(app);
            };
        }
    }
}
