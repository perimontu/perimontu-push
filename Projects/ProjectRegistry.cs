using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using CoberPush.Api.Options;
using CoberPush.Api.Security;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Projects;

/// <summary>Arma una sola vez los proyectos a partir de la configuración (ya validada al arrancar).</summary>
public sealed class ProjectRegistry : IProjectRegistry
{
    private readonly Dictionary<string, Project> _projects;

    public ProjectRegistry(IOptions<ProjectsOptions> options)
    {
        _projects = options.Value.Items
            .Select(Build)
            .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<Project> All => _projects.Values;

    public bool TryGet(string? id, [NotNullWhen(true)] out Project? project)
    {
        project = null;
        return id is not null && _projects.TryGetValue(id, out project);
    }

    private static Project Build(ProjectOptions options)
    {
        var projectIps = ParseIps(options.AllowedIps);

        return new Project
        {
            Id = options.Id,
            Name = string.IsNullOrWhiteSpace(options.Name) ? options.Id : options.Name,
            Enabled = options.Enabled,
            FirebaseProjectId = options.Firebase.ProjectId,
            CredentialsEnvVar = options.Firebase.CredentialsEnvVar,
            ApiKeys = options.ApiKeys.Select(k => BuildKey(k, projectIps, options.DefaultRateLimit)).ToList(),
            DefaultRateLimit = options.DefaultRateLimit,
            ReceiptsRateLimit = options.ReceiptsRateLimit,
            ReceiptSecret = Encoding.UTF8.GetBytes(options.Receipts.HmacSecret),
            ReceiptMaxAgeDays = options.Receipts.MaxAgeDays,
            UrlApi = options.UrlApi,
            UrlPolicy = options.UrlPolicy
        };
    }

    private static ProjectApiKey BuildKey(ApiKeyOptions key, IpAllowList projectIps, RateLimitPolicyOptions projectLimit)
    {
        return new ProjectApiKey
        {
            Name = key.Name,
            Enabled = key.Enabled,
            ExpiresAt = key.ExpiresAt,
            KeyHash = SHA256.HashData(Encoding.UTF8.GetBytes(key.Key)),
            AllowedIps = key.AllowedIps.Length > 0 ? ParseIps(key.AllowedIps) : projectIps,
            RateLimit = key.RateLimit ?? projectLimit
        };
    }

    private static IpAllowList ParseIps(string[] entries)
    {
        if (!IpAllowList.TryParse(entries, out var list, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return list;
    }
}
