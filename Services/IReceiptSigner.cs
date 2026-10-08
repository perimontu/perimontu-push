using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

public enum ReceiptVerification
{
    Valid,
    Invalid,
    Expired
}

/// <summary>
/// Firma y verifica el "recibo" (HMAC de <c>projectId</c> + <c>messageId</c> + fecha de envío) sin guardar estado.
/// Cada proyecto usa su propio secreto: un recibo de un proyecto no sirve en otro.
/// </summary>
public interface IReceiptSigner
{
    string Sign(Project project, string messageId, long sentAtUnixSeconds);

    ReceiptVerification Verify(Project project, string messageId, long sentAtUnixSeconds, string receipt);
}
