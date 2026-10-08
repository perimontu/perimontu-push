using System.Text.RegularExpressions;
using CoberPush.Api.Models;

namespace CoberPush.Api.Validation;

/// <summary>Valida el formato del reporte de lectura enviado por la app (la firma se verifica aparte).</summary>
public sealed partial class ReadReceiptValidator
{
    private const int MAX_RECEIPT_LENGTH = 128;
    private const int MAX_TOKEN_LENGTH = 4096;

    [GeneratedRegex(@"^[A-Za-z0-9_.:\-]{1,64}$")]
    private static partial Regex MessageIdRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_\-]{1,128}$")]
    private static partial Regex ReceiptRegex();

    public Dictionary<string, string[]> Validate(ReadReceiptRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.MessageId is null || !MessageIdRegex().IsMatch(request.MessageId))
        {
            errors["messageId"] = ["messageId inválido."];
        }

        if (request.SentAt <= 0)
        {
            errors["sentAt"] = ["sentAt inválido."];
        }

        if (request.Receipt is null || request.Receipt.Length > MAX_RECEIPT_LENGTH || !ReceiptRegex().IsMatch(request.Receipt))
        {
            errors["receipt"] = ["receipt inválido."];
        }

        if (string.IsNullOrWhiteSpace(request.DeviceToken) || request.DeviceToken.Length > MAX_TOKEN_LENGTH)
        {
            errors["deviceToken"] = ["deviceToken inválido."];
        }

        return errors;
    }
}
