namespace CoberPush.Api.Services;

public enum ReceiptVerification
{
    Valid,
    Invalid,
    Expired
}

/// <summary>Firma y verifica el "recibo" (HMAC de <c>messageId</c> + fecha de envío) sin guardar estado.</summary>
public interface IReceiptSigner
{
    string Sign(string messageId, long sentAtUnixSeconds);

    ReceiptVerification Verify(string messageId, long sentAtUnixSeconds, string receipt);
}
