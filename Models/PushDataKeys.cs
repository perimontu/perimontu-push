namespace CoberPush.Api.Models;

/// <summary>Claves reservadas dentro de <c>data</c> del mensaje FCM (contrato con CoberApp).</summary>
public static class PushDataKeys
{
    public const string URL = "url";
    public const string MESSAGE_ID = "messageId";
    public const string SENT_AT = "sentAt";
    public const string RECEIPT = "receipt";

    public static readonly IReadOnlySet<string> RESERVED =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { URL, MESSAGE_ID, SENT_AT, RECEIPT };
}
