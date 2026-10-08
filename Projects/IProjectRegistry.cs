using System.Diagnostics.CodeAnalysis;

namespace CoberPush.Api.Projects;

/// <summary>Consulta de proyectos por id (sin distinguir mayúsculas).</summary>
public interface IProjectRegistry
{
    IReadOnlyCollection<Project> All { get; }

    bool TryGet(string? id, [NotNullWhen(true)] out Project? project);
}
