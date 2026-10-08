using CoberPush.Api.Options;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Services;

public sealed class UrlPolicy(IOptions<PushOptions> options) : IUrlPolicy
{
    private readonly PushOptions _options = options.Value;

    public bool TryNormalize(string? url, out string? normalized)
    {
        normalized = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !IsAllowedHost(uri.Host)
            || !IsAllowedPath(uri.AbsolutePath))
        {
            return false;
        }

        normalized = new UriBuilder(uri) { Host = _options.CanonicalUrlHost, Port = -1 }.Uri.AbsoluteUri;
        return true;
    }

    private bool IsAllowedHost(string host)
    {
        return _options.AllowedUrlHosts.Any(h => h.Equals(host, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsAllowedPath(string path)
    {
        var prefix = _options.AllowedUrlPathPrefix.TrimEnd('/');
        return path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }
}
