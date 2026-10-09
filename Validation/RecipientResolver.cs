using CoberPush.Api.Models;

namespace CoberPush.Api.Validation;

/// <summary>Resultado de resolver los destinatarios de un pedido: lista válida o errores por campo.</summary>
public sealed record RecipientsResult(
    IReadOnlyList<PushRecipient> Recipients,
    Dictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Convierte <c>tokens</c> o <c>targets</c> en destinatarios, cada uno con su propio messageId: el que informa el
/// llamador o, si falta, un GUID generado. Así la lectura que reporta la app se asocia a un único envío.
/// </summary>
public sealed class RecipientResolver(PushRequestValidator validator)
{
    public RecipientsResult Resolve(SendPushRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (!string.IsNullOrWhiteSpace(request.MessageId))
        {
            errors["messageId"] = ["En /send el messageId va por destino: use targets[].messageId o deje que se genere uno por token."];
        }

        if (request.Tokens is not null && request.Targets is not null)
        {
            errors["targets"] = ["Use 'tokens' o 'targets', no ambos."];
            return new RecipientsResult([], errors);
        }

        var recipients = request.Targets is not null
            ? FromTargets(request.Targets, errors)
            : FromTokens(request.Tokens, errors);

        return new RecipientsResult(recipients, errors);
    }

    private List<PushRecipient> FromTokens(List<string>? tokens, Dictionary<string, string[]> errors)
    {
        var distinct = tokens?.Distinct().ToList();

        foreach (var (field, messages) in validator.ValidateTokens(distinct))
        {
            errors[field] = messages;
        }

        return errors.ContainsKey("tokens")
            ? []
            : distinct!.Select(t => new PushRecipient(t, NewMessageId())).ToList();
    }

    private List<PushRecipient> FromTargets(IReadOnlyList<PushTargetRequest?> targets, Dictionary<string, string[]> errors)
    {
        var tokens = targets.Select(t => t?.Token ?? string.Empty).ToList();
        var tokenErrors = validator.ValidateTokens(tokens);

        if (tokenErrors.Count > 0)
        {
            errors["targets"] = tokenErrors["tokens"];
            return [];
        }

        if (tokens.Distinct().Count() != tokens.Count)
        {
            errors["targets"] = ["Hay tokens repetidos en 'targets'."];
            return [];
        }

        return BuildRecipients(targets!, errors);
    }

    private static List<PushRecipient> BuildRecipients(IReadOnlyList<PushTargetRequest> targets, Dictionary<string, string[]> errors)
    {
        var recipients = targets
            .Select(t => new PushRecipient(t.Token!, string.IsNullOrWhiteSpace(t.MessageId) ? NewMessageId() : t.MessageId))
            .ToList();

        if (recipients.Any(r => !PushRequestValidator.IsValidMessageId(r.MessageId)))
        {
            errors["targets"] = ["Hay messageId inv\u00e1lidos: solo letras, n\u00fameros y . _ : - (m\u00e1x. 64)."];
            return [];
        }

        if (recipients.Select(r => r.MessageId).Distinct().Count() != recipients.Count)
        {
            errors["targets"] = ["Cada destino debe tener un messageId \u00fanico."];
            return [];
        }

        return recipients;
    }

    private static string NewMessageId() => Guid.NewGuid().ToString("N");
}
