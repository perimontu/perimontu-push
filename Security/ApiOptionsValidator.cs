using Microsoft.Extensions.Options;
using CoberPush.Api.Options;

namespace CoberPush.Api.Security;

/// <summary>Validación al arrancar: que los proxies de confianza de <c>appsettings</c> sean IPs parseables.</summary>
public sealed class ApiOptionsValidator : IValidateOptions<ApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        var invalidProxy = options.KnownProxies.FirstOrDefault(p => !System.Net.IPAddress.TryParse(p, out _));
        return invalidProxy is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Proxy inválido en KnownProxies: '{invalidProxy}'.");
    }
}
