using FirebaseAdmin.Messaging;

namespace CoberPush.Api.Services;

/// <summary>Traduce los códigos de error de FCM a los que expone esta API y decide si hay que borrar el token.</summary>
public static class FcmErrorMapper
{
    public static string ToCode(MessagingErrorCode? code)
    {
        return code switch
        {
            MessagingErrorCode.Unregistered => "UNREGISTERED",
            MessagingErrorCode.SenderIdMismatch => "SENDER_ID_MISMATCH",
            MessagingErrorCode.InvalidArgument => "INVALID_ARGUMENT",
            MessagingErrorCode.QuotaExceeded => "QUOTA_EXCEEDED",
            MessagingErrorCode.Unavailable => "UNAVAILABLE",
            MessagingErrorCode.Internal => "INTERNAL",
            MessagingErrorCode.ThirdPartyAuthError => "THIRD_PARTY_AUTH_ERROR",
            _ => "UNKNOWN"
        };
    }

    /// <summary>El token ya no sirve y el backend PHP debe borrarlo.</summary>
    public static bool ShouldRemoveToken(MessagingErrorCode? code)
    {
        return code is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch;
    }
}
