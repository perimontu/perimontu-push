using CoberPush.Api.Models;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

/// <summary>Reenvía al backend del proyecto el aviso de lectura de un mensaje.</summary>
public interface IReadForwarder
{
    Task<ForwardOutcome> ForwardReadAsync(Project project, ReadPayload payload, CancellationToken ct = default);
}
