using System.Security.Cryptography;
using CoberPush.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoberPush.Api.Tests;

/// <summary>
/// Cada prueba usa ids de proyecto únicos: las FirebaseApp son estáticas del proceso y no se pueden repetir por nombre.
/// Ninguna llega a la red (la inicialización de FirebaseApp es local).
/// </summary>
public class FirebaseMessagingProviderTests : IDisposable
{
    private const string ENV_VAR = "TEST_FIREBASE_CREDENTIALS";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "coberpush-tests-" + Guid.NewGuid().ToString("N"));

    public FirebaseMessagingProviderTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private static string UniqueId() => "t" + Guid.NewGuid().ToString("N")[..12];

    private string WriteServiceAccount(string firebaseProjectId, string fileName = "sa.json")
    {
        using var rsa = RSA.Create(2048);
        var json = $$"""
            {
              "type": "service_account",
              "project_id": "{{firebaseProjectId}}",
              "private_key_id": "abc123",
              "private_key": {{System.Text.Json.JsonSerializer.Serialize(rsa.ExportPkcs8PrivateKeyPem())}},
              "client_email": "test@{{firebaseProjectId}}.iam.gserviceaccount.com",
              "client_id": "1",
              "token_uri": "https://oauth2.googleapis.com/token"
            }
            """;

        var path = Path.Combine(_dir, fileName);
        File.WriteAllText(path, json);
        return path;
    }

    private static FirebaseMessagingProvider Provider(params (string Name, string? Value)[] variables)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(variables.ToDictionary(v => v.Name, v => v.Value))
            .Build();

        return new FirebaseMessagingProvider(config, NullLogger<FirebaseMessagingProvider>.Instance);
    }

    private static CoberPush.Api.Projects.Project ProjectFor(string id, string firebaseProjectId, string envVar = ENV_VAR)
    {
        return TestProjects.Create(id, o =>
        {
            o.Firebase.ProjectId = firebaseProjectId;
            o.Firebase.CredentialsEnvVar = envVar;
        });
    }

    [Fact]
    public void Variable_sin_valor_es_firebase_no_disponible()
    {
        var provider = Provider();

        var ex = Assert.Throws<FirebaseUnavailableException>(() => provider.GetMessaging(ProjectFor(UniqueId(), "fb-a")));

        Assert.Contains(ENV_VAR, ex.Message);
    }

    [Fact]
    public void Archivo_inexistente_es_firebase_no_disponible_y_no_revela_la_ruta()
    {
        var provider = Provider((ENV_VAR, Path.Combine(_dir, "no-existe.json")));

        var ex = Assert.Throws<FirebaseUnavailableException>(() => provider.GetMessaging(ProjectFor(UniqueId(), "fb-a")));

        Assert.DoesNotContain(_dir, ex.Message);
    }

    [Fact]
    public void Json_invalido_es_firebase_no_disponible()
    {
        var path = Path.Combine(_dir, "roto.json");
        File.WriteAllText(path, "esto no es json");
        var provider = Provider((ENV_VAR, path));

        Assert.Throws<FirebaseUnavailableException>(() => provider.GetMessaging(ProjectFor(UniqueId(), "fb-a")));
    }

    [Fact]
    public void Credencial_de_otro_proyecto_de_firebase_se_rechaza()
    {
        var provider = Provider((ENV_VAR, WriteServiceAccount("otro-proyecto-firebase")));

        var ex = Assert.Throws<FirebaseUnavailableException>(
            () => provider.GetMessaging(ProjectFor(UniqueId(), "proyecto-configurado")));

        Assert.Contains("otro proyecto", ex.Message);
    }

    [Fact]
    public void Credencial_correcta_crea_el_cliente_y_se_reutiliza()
    {
        var provider = Provider((ENV_VAR, WriteServiceAccount("proyecto-ok")));
        var project = ProjectFor(UniqueId(), "proyecto-ok");

        var first = provider.GetMessaging(project);
        var second = provider.GetMessaging(project);

        Assert.Same(first, second);
    }

    [Fact]
    public void Cada_proyecto_usa_su_propia_credencial_y_cliente()
    {
        var provider = Provider(
            ("CRED_A", WriteServiceAccount("firebase-a", "a.json")),
            ("CRED_B", WriteServiceAccount("firebase-b", "b.json")));

        var a = provider.GetMessaging(ProjectFor(UniqueId(), "firebase-a", "CRED_A"));
        var b = provider.GetMessaging(ProjectFor(UniqueId(), "firebase-b", "CRED_B"));

        Assert.NotSame(a, b);
    }

    [Fact]
    public void Un_proyecto_roto_no_afecta_a_otro()
    {
        var provider = Provider(("CRED_OK", WriteServiceAccount("firebase-ok")));

        Assert.Throws<FirebaseUnavailableException>(
            () => provider.GetMessaging(ProjectFor(UniqueId(), "firebase-roto", "CRED_FALTANTE")));
        var ok = provider.GetMessaging(ProjectFor(UniqueId(), "firebase-ok", "CRED_OK"));

        Assert.NotNull(ok);
    }

    [Fact]
    public void Tras_corregir_la_credencial_el_proximo_pedido_funciona()
    {
        var path = Path.Combine(_dir, "luego.json");
        var provider = Provider((ENV_VAR, path));
        var project = ProjectFor(UniqueId(), "firebase-luego");

        Assert.Throws<FirebaseUnavailableException>(() => provider.GetMessaging(project));

        File.Copy(WriteServiceAccount("firebase-luego", "origen.json"), path);

        Assert.NotNull(provider.GetMessaging(project));
    }
}
