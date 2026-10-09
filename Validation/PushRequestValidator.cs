using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CoberPush.Api.Models;
using CoberPush.Api.Options;
using CoberPush.Api.Projects;
using CoberPush.Api.Services;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Validation;

/// <summary>Valida los pedidos de envío y arma el <see cref="PushContent"/> firmado.</summary>
public sealed partial class PushRequestValidator(
    IOptions<PushOptions> options,
    IUrlPolicy urlPolicy,
    IReceiptSigner signer,
    TimeProvider clock)
{
    private const int MAX_TITLE_LENGTH = 150;
    private const int MAX_BODY_LENGTH = 1000;
    private const int MAX_DATA_ENTRIES = 20;
    private const int MAX_DATA_VALUE_LENGTH = 500;
    private const int MAX_URL_LENGTH = 2000;
    private const int MAX_TTL_SECONDS = 2_419_200;
    private const int MAX_PAYLOAD_BYTES = 4000; // FCM admite 4096; se deja margen para el resto del sobre
    private const int MAX_MESSAGE_ID_LENGTH = 64;

    private readonly PushOptions _options = options.Value;

    [GeneratedRegex(@"^[A-Za-z0-9_.:\-]{1,64}$")]
    private static partial Regex MessageIdRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{1,64}$")]
    private static partial Regex DataKeyRegex();

    [GeneratedRegex(@"^[A-Za-z0-9\-_.~%]{1,900}$")]
    private static partial Regex TopicRegex();

    [GeneratedRegex(@"^\S{1,4096}$")]
    private static partial Regex TokenRegex();

    public PushContentBuildResult BuildContent(Project project, PushContentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var title = ValidateText(request.Title, "title", MAX_TITLE_LENGTH, errors);
        var body = ValidateText(request.Body, "body", MAX_BODY_LENGTH, errors);
        var url = ValidateUrl(project, request.Url, errors);
        var imageUrl = ValidateImageUrl(request.ImageUrl, errors);
        var messageId = ValidateMessageId(request.MessageId, errors);
        var data = ValidateData(request.Data, errors);
        var ttl = ValidateTtl(request.TtlSeconds, errors);

        if (errors.Count > 0)
        {
            return new PushContentBuildResult { Errors = errors };
        }

        var sentAt = clock.GetUtcNow().ToUnixTimeSeconds();
        var content = new PushContent(
            messageId, title!, body!, url, imageUrl, sentAt, signer.Sign(project, messageId, sentAt), data, ttl);

        return CheckPayloadSize(content, errors);
    }

    /// <summary>Formato permitido de un messageId (letras, números y . _ : -, hasta 64).</summary>
    public static bool IsValidMessageId(string messageId) => MessageIdRegex().IsMatch(messageId);

    /// <summary>Copia el contenido para un destino: su propio messageId y su propio recibo firmado.</summary>
    public PushContent Personalize(Project project, PushContent content, string messageId)
    {
        return content with { MessageId = messageId, Receipt = signer.Sign(project, messageId, content.SentAt) };
    }

    public Dictionary<string, string[]> ValidateTokens(IReadOnlyCollection<string>? tokens)
    {
        var errors = new Dictionary<string, string[]>();

        if (tokens is null || tokens.Count == 0)
        {
            errors["tokens"] = ["Debe incluir al menos un token."];
        }
        else if (tokens.Count > _options.MaxTokensPerRequest)
        {
            errors["tokens"] = [$"Máximo {_options.MaxTokensPerRequest} tokens por pedido."];
        }
        else if (tokens.Any(t => !TokenRegex().IsMatch(t ?? string.Empty)))
        {
            errors["tokens"] = ["Hay tokens vacíos, con espacios o demasiado largos."];
        }

        return errors;
    }

    public Dictionary<string, string[]> ValidateTopic(string? topic)
    {
        var errors = new Dictionary<string, string[]>();

        if (topic is null || !TopicRegex().IsMatch(topic))
        {
            errors["topic"] = ["Topic inválido."];
        }

        return errors;
    }

    private static string? ValidateText(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            errors[field] = [$"'{field}' es obligatorio."];
            return null;
        }

        if (text.Length > maxLength)
        {
            errors[field] = [$"'{field}' admite hasta {maxLength} caracteres."];
            return null;
        }

        return text;
    }

    private string? ValidateUrl(Project project, string? url, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (url.Length <= MAX_URL_LENGTH && urlPolicy.TryNormalize(project, url, out var normalized))
        {
            return normalized;
        }

        var policy = project.UrlPolicy;
        errors["url"] = [$"La URL debe ser https://{policy.CanonicalHost}{policy.PathPrefix}..."];
        return null;
    }

    private static string? ValidateImageUrl(string? imageUrl, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (imageUrl.Length <= MAX_URL_LENGTH
            && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri.AbsoluteUri;
        }

        errors["imageUrl"] = ["La imagen debe ser una URL https válida."];
        return null;
    }

    private static string ValidateMessageId(string? messageId, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return Guid.NewGuid().ToString("N");
        }

        if (!MessageIdRegex().IsMatch(messageId))
        {
            errors["messageId"] = ["Solo letras, números y . _ : - (máx. 64)."];
        }

        return messageId;
    }

    private static Dictionary<string, string> ValidateData(
        Dictionary<string, string>? data, Dictionary<string, string[]> errors)
    {
        var result = new Dictionary<string, string>();

        if (data is null || data.Count == 0)
        {
            return result;
        }

        if (data.Count > MAX_DATA_ENTRIES)
        {
            errors["data"] = [$"Máximo {MAX_DATA_ENTRIES} entradas."];
            return result;
        }

        foreach (var (key, value) in data)
        {
            var error = DataEntryError(key, value);
            if (error is not null)
            {
                errors["data"] = [error];
                return result;
            }

            result[key] = value;
        }

        return result;
    }

    private static string? DataEntryError(string key, string? value)
    {
        if (!DataKeyRegex().IsMatch(key))
        {
            return $"Clave inválida: '{key}'.";
        }

        if (PushDataKeys.RESERVED.Contains(key))
        {
            return $"La clave '{key}' está reservada.";
        }

        if (key.StartsWith("google", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("gcm", StringComparison.OrdinalIgnoreCase)
            || key.Equals("from", StringComparison.OrdinalIgnoreCase))
        {
            return $"La clave '{key}' está reservada por FCM.";
        }

        return value is null || value.Length > MAX_DATA_VALUE_LENGTH
            ? $"El valor de '{key}' es nulo o supera {MAX_DATA_VALUE_LENGTH} caracteres."
            : null;
    }

    private int ValidateTtl(int? ttlSeconds, Dictionary<string, string[]> errors)
    {
        if (ttlSeconds is null)
        {
            return _options.DefaultTtlSeconds;
        }

        if (ttlSeconds is < 0 or > MAX_TTL_SECONDS)
        {
            errors["ttlSeconds"] = [$"Debe estar entre 0 y {MAX_TTL_SECONDS}."];
        }

        return ttlSeconds.Value;
    }

    private static PushContentBuildResult CheckPayloadSize(PushContent content, Dictionary<string, string[]> errors)
    {
        var size = Encoding.UTF8.GetByteCount(content.Title)
            + Encoding.UTF8.GetByteCount(content.Body)
            + Encoding.UTF8.GetByteCount(content.Url ?? string.Empty)
            + Encoding.UTF8.GetByteCount(content.ImageUrl ?? string.Empty)
            + MAX_MESSAGE_ID_LENGTH // peor caso: cada destino puede llevar su propio messageId
            + Encoding.UTF8.GetByteCount(content.Receipt)
            + content.SentAt.ToString(CultureInfo.InvariantCulture).Length
            + content.Data.Sum(d => Encoding.UTF8.GetByteCount(d.Key) + Encoding.UTF8.GetByteCount(d.Value));

        if (size > MAX_PAYLOAD_BYTES)
        {
            errors["payload"] = [$"El contenido supera el máximo de {MAX_PAYLOAD_BYTES} bytes de FCM."];
            return new PushContentBuildResult { Errors = errors };
        }

        return new PushContentBuildResult { Content = content };
    }
}
