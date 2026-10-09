using CoberPush.Api.Models;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

/// <summary>Abstracción del envío (permite probar los endpoints sin Firebase). Cada llamada indica el proyecto.</summary>
public interface IPushSender
{
    Task<PushSendResult> SendToTokensAsync(
        Project project, IReadOnlyList<TokenMessage> messages, bool dryRun, CancellationToken ct = default);

    Task<PushSendResult> SendToTopicAsync(
        Project project, PushContent content, string topic, bool dryRun, CancellationToken ct = default);

    /// <summary>
    /// Comprueba conexión, credencial y proyecto de Firebase con un envío de prueba (dryRun, no entrega nada).
    /// Lanza <see cref="FirebaseUnavailableException"/> si no se puede inicializar Firebase.
    /// </summary>
    Task<bool> CheckConnectivityAsync(Project project, CancellationToken ct = default);
}
