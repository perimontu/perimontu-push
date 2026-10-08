using CoberPush.Api.Projects;
using FirebaseAdmin.Messaging;

namespace CoberPush.Api.Services;

/// <summary>Entrega el cliente de FCM de cada proyecto (creado de forma perezosa y cacheado).</summary>
public interface IFirebaseMessagingProvider
{
    /// <exception cref="FirebaseUnavailableException">Falta la credencial del proyecto o no se pudo inicializar.</exception>
    FirebaseMessaging GetMessaging(Project project);
}
