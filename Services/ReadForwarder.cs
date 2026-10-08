using System.Net;
using CoberPush.Api.Models;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

/// <summary>Reenvía el aviso de lectura al backend de cada proyecto, con su URL, bearer, timeout y reintentos.</summary>
public sealed class ReadForwarder(HttpClient http, ILogger<ReadForwarder> logger) : IReadForwarder
{
    private const int RETRY_BASE_DELAY_MS = 300;

    public async Task<ForwardOutcome> ForwardReadAsync(Project project, ReadPayload payload, CancellationToken ct = default)
    {
        var url = BuildUrl(project);
        var retries = project.UrlApi.RetryCount;

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            var outcome = await TrySendAsync(project, url, payload, ct);

            if (outcome != ForwardOutcome.Unavailable)
            {
                return outcome;
            }

            if (attempt < retries)
            {
                await Task.Delay(RETRY_BASE_DELAY_MS * (attempt + 1), ct);
            }
        }

        return ForwardOutcome.Unavailable;
    }

    private static Uri BuildUrl(Project project)
    {
        return new Uri(project.UrlApi.BaseUrl.TrimEnd('/') + "/" + project.UrlApi.ReadPath.TrimStart('/'));
    }

    /// <summary>Un intento. 5xx, timeout y fallas de red → Unavailable (reintentable); 4xx → Rejected.</summary>
    private async Task<ForwardOutcome> TrySendAsync(Project project, Uri url, ReadPayload payload, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(project.UrlApi.TimeoutSeconds));

        try
        {
            using var request = BuildRequest(project, url, payload);
            using var response = await http.SendAsync(request, timeout.Token);
            return Classify(project, response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "No se pudo contactar la API de destino de {Project} ({Host})", project.Id, url.Host);
            return ForwardOutcome.Unavailable;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Timeout al contactar la API de destino de {Project} ({Host})", project.Id, url.Host);
            return ForwardOutcome.Unavailable;
        }
    }

    private static HttpRequestMessage BuildRequest(Project project, Uri url, ReadPayload payload)
    {
        return new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", project.UrlApi.BearerToken) }
        };
    }

    private ForwardOutcome Classify(Project project, HttpStatusCode status)
    {
        var code = (int)status;

        if (code is >= 200 and < 300)
        {
            return ForwardOutcome.Accepted;
        }

        logger.LogWarning("La API de destino de {Project} respondió {Status} al reporte de lectura", project.Id, code);
        return code >= 500 || status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests
            ? ForwardOutcome.Unavailable
            : ForwardOutcome.Rejected;
    }
}
