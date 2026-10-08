using System.Net;
using CoberPush.Api.Models;
using CoberPush.Api.Options;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Services;

public sealed class PhpReadForwarder : IPhpReadForwarder
{
    private const int RETRY_BASE_DELAY_MS = 300;

    private readonly HttpClient _http;
    private readonly PhpApiOptions _options;
    private readonly ILogger<PhpReadForwarder> _logger;

    public PhpReadForwarder(HttpClient http, IOptions<PhpApiOptions> options, ILogger<PhpReadForwarder> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ForwardOutcome> ForwardReadAsync(PhpReadPayload payload, CancellationToken ct = default)
    {
        var url = BuildUrl();

        for (var attempt = 0; attempt <= _options.RetryCount; attempt++)
        {
            var outcome = await TrySendAsync(url, payload, ct);

            if (outcome != ForwardOutcome.Unavailable)
            {
                return outcome;
            }

            if (attempt < _options.RetryCount)
            {
                await Task.Delay(RETRY_BASE_DELAY_MS * (attempt + 1), ct);
            }
        }

        return ForwardOutcome.Unavailable;
    }

    private Uri BuildUrl()
    {
        return new Uri(_options.BaseUrl.TrimEnd('/') + "/" + _options.ReadPath.TrimStart('/'));
    }

    /// <summary>Un intento. 5xx, timeout y fallas de red → Unavailable (reintentable); 4xx → Rejected.</summary>
    private async Task<ForwardOutcome> TrySendAsync(Uri url, PhpReadPayload payload, CancellationToken ct)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(url, payload, ct);
            return Classify(response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo contactar la API PHP ({Host})", url.Host);
            return ForwardOutcome.Unavailable;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Timeout al contactar la API PHP ({Host})", url.Host);
            return ForwardOutcome.Unavailable;
        }
    }

    private ForwardOutcome Classify(HttpStatusCode status)
    {
        var code = (int)status;

        if (code is >= 200 and < 300)
        {
            return ForwardOutcome.Accepted;
        }

        _logger.LogWarning("La API PHP respondió {Status} al reporte de lectura", code);
        return code >= 500 || status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests
            ? ForwardOutcome.Unavailable
            : ForwardOutcome.Rejected;
    }
}
