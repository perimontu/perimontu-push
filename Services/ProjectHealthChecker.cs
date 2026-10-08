using System.Collections.Concurrent;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

/// <summary>
/// Comprobación con caché de 60 s por proyecto: aunque se llame al endpoint en bucle, contra Google se hace
/// como máximo una prueba por minuto y proyecto.
/// </summary>
public sealed class ProjectHealthChecker(IPushSender sender, TimeProvider clock, ILogger<ProjectHealthChecker> logger)
    : IProjectHealthChecker
{
    private const int CACHE_SECONDS = 60;

    private readonly ConcurrentDictionary<string, ProjectHealthResult> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ProjectHealthResult> CheckAsync(Project project, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        if (_cache.TryGetValue(project.Id, out var cached) && now - cached.CheckedAt < TimeSpan.FromSeconds(CACHE_SECONDS))
        {
            return cached;
        }

        var result = new ProjectHealthResult(project.Id, await ProbeAsync(project, ct), now);
        _cache[project.Id] = result;
        return result;
    }

    private async Task<bool> ProbeAsync(Project project, CancellationToken ct)
    {
        try
        {
            return await sender.CheckConnectivityAsync(project, ct);
        }
        catch (FirebaseUnavailableException ex)
        {
            logger.LogWarning(ex, "Firebase no disponible para el proyecto {Project}", project.Id);
            return false;
        }
    }
}
