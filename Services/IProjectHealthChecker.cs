using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

/// <summary>Estado de la conexión de un proyecto con Firebase (con la hora en que se comprobó).</summary>
public sealed record ProjectHealthResult(string ProjectId, bool FirebaseOk, DateTimeOffset CheckedAt);

/// <summary>Comprueba la conexión con Firebase de un proyecto. El resultado se cachea para no estresar a Google.</summary>
public interface IProjectHealthChecker
{
    Task<ProjectHealthResult> CheckAsync(Project project, CancellationToken ct = default);
}
