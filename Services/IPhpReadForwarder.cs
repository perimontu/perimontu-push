using CoberPush.Api.Models;

namespace CoberPush.Api.Services;

/// <summary>Reenvía a la API PHP el aviso de lectura de un mensaje.</summary>
public interface IPhpReadForwarder
{
    Task<ForwardOutcome> ForwardReadAsync(PhpReadPayload payload, CancellationToken ct = default);
}
