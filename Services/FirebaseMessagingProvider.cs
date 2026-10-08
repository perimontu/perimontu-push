using System.Collections.Concurrent;
using CoberPush.Api.Projects;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace CoberPush.Api.Services;

/// <summary>
/// Crea una <see cref="FirebaseApp"/> por proyecto (nombrada con el id del proyecto) la primera vez que se la necesita.
/// Así la API arranca y responde aunque falte la credencial de un proyecto, y un proyecto roto no afecta a los demás.
/// La ruta del JSON se lee de la variable de entorno indicada en <c>Firebase.CredentialsEnvVar</c>.
/// </summary>
public sealed class FirebaseMessagingProvider(IConfiguration config, ILogger<FirebaseMessagingProvider> logger)
    : IFirebaseMessagingProvider
{
    private readonly ConcurrentDictionary<string, FirebaseMessaging> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _createLock = new();

    public FirebaseMessaging GetMessaging(Project project)
    {
        if (_cache.TryGetValue(project.Id, out var cached))
        {
            return cached;
        }

        lock (_createLock)
        {
            if (_cache.TryGetValue(project.Id, out cached))
            {
                return cached;
            }

            // Si Create lanza, nada queda cacheado y el próximo pedido reintenta.
            var messaging = Create(project);
            _cache[project.Id] = messaging;
            return messaging;
        }
    }

    private FirebaseMessaging Create(Project project)
    {
        var path = ResolveCredentialPath(project);

        try
        {
            var serviceAccount = CredentialFactory.FromFile<ServiceAccountCredential>(path);

            if (!string.Equals(serviceAccount.ProjectId, project.FirebaseProjectId, StringComparison.Ordinal))
            {
                throw new FirebaseUnavailableException(
                    $"La credencial del proyecto '{project.Id}' pertenece a otro proyecto de Firebase.");
            }

            var app = FirebaseApp.GetInstance(project.Id) ?? FirebaseApp.Create(new AppOptions
            {
                Credential = serviceAccount.ToGoogleCredential(),
                ProjectId = project.FirebaseProjectId
            }, project.Id);

            return FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception ex) when (ex is InvalidOperationException or AggregateException or IOException
            or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            logger.LogError(ex, "No se pudo inicializar Firebase del proyecto {Project}", project.Id);
            throw new FirebaseUnavailableException(
                $"No se pudo inicializar Firebase del proyecto '{project.Id}': revise el JSON de la credencial.", ex);
        }
    }

    private string ResolveCredentialPath(Project project)
    {
        var path = config[project.CredentialsEnvVar];

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new FirebaseUnavailableException(
                $"La variable de entorno '{project.CredentialsEnvVar}' del proyecto '{project.Id}' no está definida.");
        }

        if (!File.Exists(path))
        {
            throw new FirebaseUnavailableException(
                $"El archivo de credencial del proyecto '{project.Id}' (variable '{project.CredentialsEnvVar}') no existe.");
        }

        return path;
    }
}
