using CoberPush.Api.Options;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

public sealed class UrlPolicy : IUrlPolicy
{
    public bool TryNormalize(Project project, string? url, out string? normalized)
    {
        normalized = null;
        var policy = project.UrlPolicy;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !IsAllowedHost(policy, uri.Host)
            || !IsAllowedPath(policy, uri.AbsolutePath))
        {
            return false;
        }

        normalized = new UriBuilder(uri) { Host = policy.CanonicalHost, Port = -1 }.Uri.AbsoluteUri;
        return true;
    }

    private static bool IsAllowedHost(UrlPolicyOptions policy, string host)
    {
        return policy.AllowedHosts.Any(h => h.Equals(host, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllowedPath(UrlPolicyOptions policy, string path)
    {
        var prefix = policy.PathPrefix.TrimEnd('/');
        return path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }
}
