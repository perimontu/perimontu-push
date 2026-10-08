using FirebaseAdmin.Messaging;

namespace CoberPush.Api.Services;

/// <summary>Traduce los códigos de error de FCM a los que expone esta API y decide si hay que borrar el token.</summary>
public static class FcmErrorMapper
{
    /// <summary>El token no existe para el proyecto con el que se envió.</summary>
    public const string NOT_IN_PROJECT_CODE = "NO_EXISTE";

    public static string ToCode(MessagingErrorCode? code)
    {
        return code switch
        {
            MessagingErrorCode.Unregistered => "UNREGISTERED",
            MessagingErrorCode.SenderIdMismatch => NOT_IN_PROJECT_CODE,
            MessagingErrorCode.InvalidArgument => "INVALID_ARGUMENT",
            MessagingErrorCode.QuotaExceeded => "QUOTA_EXCEEDED",
            MessagingErrorCode.Unavailable => "UNAVAILABLE",
            MessagingErrorCode.Internal => "INTERNAL",
            MessagingErrorCode.ThirdPartyAuthError => "THIRD_PARTY_AUTH_ERROR",
            _ => "UNKNOWN"
        };
    }

    /// <summary>
    /// El token ya no sirve y el backend debe borrarlo. <c>SENDER_ID_MISMATCH</c> NO entra: significa que el token
    /// pertenece a otro proyecto de Firebase (p. ej. un projectId equivocado), no que haya caducado.
    /// </summary>
    public static bool ShouldRemoveToken(MessagingErrorCode? code)
    {
        return code is MessagingErrorCode.Unregistered;
    }
}
