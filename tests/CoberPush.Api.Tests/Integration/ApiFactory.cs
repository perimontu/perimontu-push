using System.Net;
using CoberPush.Api.Models;
using CoberPush.Api.Projects;
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
    public List<string> SentProjects { get; } = [];
    public bool? LastDryRun { get; private set; }
    public string? LastTopic { get; private set; }

    public bool ConnectivityResult { get; set; } = true;
    public Exception? ConnectivityFailure { get; set; }
    public int ConnectivityCalls { get; private set; }

    public Task<PushSendResult> SendToTokensAsync(
        Project project, IReadOnlyList<TokenMessage> messages, bool dryRun, CancellationToken ct = default)
    {
        Sent.AddRange(messages.Select(m => m.Content));
        SentProjects.Add(project.Id);
        LastDryRun = dryRun;

        var results = messages
            .Select(m => m.Token == UNREGISTERED_TOKEN
                ? new PushTargetResult(TokenMasker.Mask(m.Token), m.Content.MessageId, false, null, "UNREGISTERED", true)
                : new PushTargetResult(TokenMasker.Mask(m.Token), m.Content.MessageId, true, $"projects/p/messages/{Guid.NewGuid():N}", null, false))
            .ToList();

        var ok = results.Count(r => r.Success);
        return Task.FromResult(new PushSendResult(project.Id, ok, results.Count - ok, results));
    }

    public Task<PushSendResult> SendToTopicAsync(
        Project project, PushContent content, string topic, bool dryRun, CancellationToken ct = default)
    {
        Sent.Add(content);
        SentProjects.Add(project.Id);
        LastTopic = topic;
        LastDryRun = dryRun;

        var result = new PushTargetResult($"topic:{topic}", content.MessageId, true, "projects/p/messages/1", null, false);
        return Task.FromResult(new PushSendResult(project.Id, 1, 0, [result]));
    }

    public Task<bool> CheckConnectivityAsync(Project project, CancellationToken ct = default)
    {
        ConnectivityCalls++;
        return ConnectivityFailure is null ? Task.FromResult(ConnectivityResult) : throw ConnectivityFailure;
    }
}

public sealed class FakeReadForwarder : IReadForwarder
{
    public ForwardOutcome Outcome { get; set; } = ForwardOutcome.Accepted;
    public List<ReadPayload> Received { get; } = [];
    public List<string> ReceivedProjects { get; } = [];

    public Task<ForwardOutcome> ForwardReadAsync(Project project, ReadPayload payload, CancellationToken ct = default)
    {
        Received.Add(payload);
        ReceivedProjects.Add(project.Id);
        return Task.FromResult(Outcome);
    }
}

/// <summary>
/// API completa en memoria con Firebase y backend de destino simulados y dos proyectos (<c>cober</c> y <c>otro</c>) más
/// uno deshabilitado (<c>pausado</c>). La IP del cliente se controla con la cabecera
/// <see cref="REMOTE_IP_HEADER"/> (TestServer no tiene IP remota por sí mismo).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string REMOTE_IP_HEADER = "X-Test-Remote-Ip";
    public const string PROJECT = "cober";
    public const string OTHER_PROJECT = "otro";
    public const string DISABLED_PROJECT = "pausado";

    public const string API_KEY = "test-api-key-0123456789-abcdefghijklmnop";
    public const string RESTRICTED_KEY = "test-restricted-key-0123456789-abcdefghijk";
    public const string DISABLED_KEY = "test-disabled-key-0123456789-abcdefghijklm";
    public const string EXPIRED_KEY = "test-expired-key-0123456789-abcdefghijklmn";
    public const string LIMITED_KEY = "test-limited-key-0123456789-abcdefghijklmn";
    public const string OTHER_KEY = "test-otro-key-0123456789-abcdefghijklmnopq";
    public const string DISABLED_PROJECT_KEY = "test-pausado-key-0123456789-abcdefghijklm";
    public const string OTHER_HMAC_SECRET = "otro-hmac-secret-0123456789-abcdefghijkl";

    public const string ALLOWED_IP = "203.0.113.10";
    public const string RESTRICTED_IP = "192.0.2.5";

    private readonly Dictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _configureServices;

    public ApiFactory(Dictionary<string, string?>? overrides = null, Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
        _settings = new Dictionary<string, string?> { ["Api:KnownProxies:0"] = "127.0.0.1" };

        AddProject(0, PROJECT, "cober-test", TestProjects.HMAC_SECRET, "https://backend.test/api/push", ALLOWED_IP, "198.51.100.0/24");
        Set("Projects:0:ApiKeys:0:Name", "backend");
        Set("Projects:0:ApiKeys:0:Key", API_KEY);
        Set("Projects:0:ApiKeys:1:Name", "restringida");
        Set("Projects:0:ApiKeys:1:Key", RESTRICTED_KEY);
        Set("Projects:0:ApiKeys:1:AllowedIps:0", RESTRICTED_IP);
        Set("Projects:0:ApiKeys:2:Name", "deshabilitada");
        Set("Projects:0:ApiKeys:2:Key", DISABLED_KEY);
        Set("Projects:0:ApiKeys:2:Enabled", "false");
        Set("Projects:0:ApiKeys:3:Name", "vencida");
        Set("Projects:0:ApiKeys:3:Key", EXPIRED_KEY);
        Set("Projects:0:ApiKeys:3:ExpiresAt", "2026-10-01T00:00:00-03:00");
        Set("Projects:0:ApiKeys:4:Name", "limitada");
        Set("Projects:0:ApiKeys:4:Key", LIMITED_KEY);
        Set("Projects:0:ApiKeys:4:RateLimit:PermitLimit", "2");
        Set("Projects:0:ApiKeys:4:RateLimit:WindowSeconds", "60");

        AddProject(1, OTHER_PROJECT, "otro-test", OTHER_HMAC_SECRET, "https://otro.test/api", ALLOWED_IP);
        Set("Projects:1:ApiKeys:0:Name", "backend-otro");
        Set("Projects:1:ApiKeys:0:Key", OTHER_KEY);

        AddProject(2, DISABLED_PROJECT, "pausado-test", TestProjects.HMAC_SECRET, "https://pausado.test/api", ALLOWED_IP);
        Set("Projects:2:Enabled", "false");
        Set("Projects:2:ApiKeys:0:Name", "backend-pausado");
        Set("Projects:2:ApiKeys:0:Key", DISABLED_PROJECT_KEY);

        foreach (var (key, value) in overrides ?? [])
        {
            _settings[key] = value;
        }
    }

    public FakePushSender Sender { get; } = new();
    public FakeReadForwarder Forwarder { get; } = new();
    public FakeClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development" cargaría el appsettings.Development.json de la máquina y haría depender las pruebas del equipo.
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, RemoteIpStartupFilter>();
            services.AddSingleton<IPushSender>(Sender);
            services.AddSingleton<IReadForwarder>(Forwarder);
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

    private void AddProject(int index, string id, string firebaseId, string hmac, string urlApi, params string[] ips)
    {
        var p = $"Projects:{index}";
        Set($"{p}:Id", id);
        Set($"{p}:Name", id);
        Set($"{p}:Firebase:ProjectId", firebaseId);
        Set($"{p}:Firebase:CredentialsEnvVar", "COBER_TEST_CREDENTIALS");
        Set($"{p}:Receipts:HmacSecret", hmac);
        Set($"{p}:UrlApi:BaseUrl", urlApi);
        Set($"{p}:UrlApi:BearerToken", "bearer-de-prueba");
        Set($"{p}:UrlPolicy:AllowedHosts:0", "www.cober.com.ar");
        Set($"{p}:UrlPolicy:AllowedHosts:1", "cober.com.ar");
        Set($"{p}:UrlPolicy:CanonicalHost", "www.cober.com.ar");
        Set($"{p}:UrlPolicy:PathPrefix", "/app");

        for (var i = 0; i < ips.Length; i++)
        {
            Set($"{p}:AllowedIps:{i}", ips[i]);
        }
    }

    private void Set(string key, string value) => _settings[key] = value;

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
