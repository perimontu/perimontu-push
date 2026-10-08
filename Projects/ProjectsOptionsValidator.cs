using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CoberPush.Api.Options;
using CoberPush.Api.Security;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Projects;

/// <summary>
/// Validación al arrancar de todo el array <c>Projects</c>. Cada error indica el proyecto y el campo,
/// y se informan todos juntos para corregir la configuración de una sola vez.
/// </summary>
public sealed partial class ProjectsOptionsValidator : IValidateOptions<ProjectsOptions>
{
    [GeneratedRegex("^[A-Za-z0-9]{1,32}$")]
    private static partial Regex ProjectIdRegex();

    public ValidateOptionsResult Validate(string? name, ProjectsOptions options)
    {
        var errors = new List<string>();

        if (options.Items.Count == 0)
        {
            errors.Add("Projects: debe definirse al menos un proyecto.");
        }
        else if (!options.Items.Any(p => p.Enabled))
        {
            errors.Add("Projects: al menos un proyecto debe estar habilitado.");
        }

        CheckDuplicateIds(options.Items, errors);
        CheckDuplicateKeys(options.Items, errors);

        for (var i = 0; i < options.Items.Count; i++)
        {
            ValidateProject(options.Items[i], $"Projects[{i}] ('{options.Items[i].Id}')", errors);
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void CheckDuplicateIds(List<ProjectOptions> projects, List<string> errors)
    {
        foreach (var group in projects.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            errors.Add($"Projects: el id '{group.Key}' está repetido (se compara sin distinguir mayúsculas).");
        }
    }

    /// <summary>Una misma key no puede existir en dos consumidores: identificaría a ambos.</summary>
    private static void CheckDuplicateKeys(List<ProjectOptions> projects, List<string> errors)
    {
        var repeated = projects
            .SelectMany(p => p.ApiKeys.Select(k => (Project: p.Id, k.Name, k.Key)))
            .Where(k => !string.IsNullOrEmpty(k.Key))
            .GroupBy(k => k.Key)
            .Where(g => g.Count() > 1);

        foreach (var group in repeated)
        {
            var owners = string.Join(", ", group.Select(k => $"{k.Project}/{k.Name}"));
            errors.Add($"Projects: la misma API key está repetida en {owners}.");
        }
    }

    private static void ValidateProject(ProjectOptions project, string path, List<string> errors)
    {
        if (!ProjectIdRegex().IsMatch(project.Id ?? string.Empty))
        {
            errors.Add($"{path}.Id: debe ser alfanumérico (A-Z, 0-9) de 1 a 32 caracteres.");
        }

        ValidateObject(project.Firebase, $"{path}.Firebase", errors);
        ValidateObject(project.DefaultRateLimit, $"{path}.DefaultRateLimit", errors);
        ValidateObject(project.ReceiptsRateLimit, $"{path}.ReceiptsRateLimit", errors);
        ValidateObject(project.Receipts, $"{path}.Receipts", errors);
        ValidateObject(project.UrlApi, $"{path}.UrlApi", errors);
        ValidateObject(project.UrlPolicy, $"{path}.UrlPolicy", errors);
        ValidateUrlApi(project.UrlApi, path, errors);
        ValidateIps(project.AllowedIps, $"{path}.AllowedIps", errors);
        ValidateKeys(project.ApiKeys, path, errors);
    }

    private static void ValidateUrlApi(UrlApiOptions urlApi, string path, List<string> errors)
    {
        var valid = Uri.TryCreate(urlApi.BaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        if (!valid)
        {
            errors.Add($"{path}.UrlApi.BaseUrl: debe ser una URL http(s) absoluta.");
        }
    }

    private static void ValidateKeys(List<ApiKeyOptions> keys, string path, List<string> errors)
    {
        foreach (var group in keys.GroupBy(k => k.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            errors.Add($"{path}.ApiKeys: el nombre '{group.Key}' está repetido.");
        }

        for (var i = 0; i < keys.Count; i++)
        {
            var keyPath = $"{path}.ApiKeys[{i}] ('{keys[i].Name}')";
            ValidateObject(keys[i], keyPath, errors);
            ValidateIps(keys[i].AllowedIps, $"{keyPath}.AllowedIps", errors);

            if (keys[i].RateLimit is { } limit)
            {
                ValidateObject(limit, $"{keyPath}.RateLimit", errors);
            }
        }
    }

    private static void ValidateIps(string[] entries, string path, List<string> errors)
    {
        if (!IpAllowList.TryParse(entries, out _, out var error))
        {
            errors.Add($"{path}: {error}");
        }
    }

    /// <summary>Aplica los atributos DataAnnotations (Required, Range, MinLength) sin mostrar nunca el valor.</summary>
    private static void ValidateObject(object value, string path, List<string> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);

        foreach (var result in results)
        {
            var fields = string.Join(", ", result.MemberNames);
            errors.Add($"{path}.{fields}: {result.ErrorMessage}");
        }
    }
}
