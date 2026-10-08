using Microsoft.Extensions.Options;
using CoberPush.Api.Options;

namespace CoberPush.Api.Security;

/// <summary>Validación al arrancar: que las IPs/CIDR y los proxies de <c>appsettings</c> sean parseables.</summary>
public sealed class ApiOptionsValidator : IValidateOptions<ApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        if (!IpAllowList.TryParse(options.AllowedSendIps, out _, out var error))
        {
            return ValidateOptionsResult.Fail(error!);
        }

        var invalidProxy = options.KnownProxies.FirstOrDefault(p => !System.Net.IPAddress.TryParse(p, out _));
        return invalidProxy is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Proxy inválido en KnownProxies: '{invalidProxy}'.");
    }
}
