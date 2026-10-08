using System.Net;
using CoberPush.Api.Options;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Tests;

public class ProjectRegistryTests
{
    private static ProjectRegistry Registry(params ProjectOptions[] projects)
    {
        var all = new ProjectsOptions();
        all.Items.AddRange(projects);
        return new ProjectRegistry(Opt.Of(all));
    }

    [Fact]
    public void Busca_el_proyecto_sin_distinguir_mayusculas()
    {
        var registry = Registry(TestProjects.Options("Cober"));

        Assert.True(registry.TryGet("cober", out var project));
        Assert.Equal("Cober", project.Id);
        Assert.True(registry.TryGet("COBER", out _));
    }

    [Theory]
    [InlineData("otro")]
    [InlineData("")]
    [InlineData(null)]
    public void Proyecto_desconocido_no_se_encuentra(string? id)
    {
        Assert.False(Registry(TestProjects.Options()).TryGet(id, out _));
    }

    [Fact]
    public void Sin_nombre_se_usa_el_id()
    {
        var registry = Registry(TestProjects.Options("cober", o => o.Name = ""));

        registry.TryGet("cober", out var project);

        Assert.Equal("cober", project!.Name);
    }

    [Fact]
    public void La_key_sin_ips_propias_hereda_las_del_proyecto()
    {
        var registry = Registry(TestProjects.Options());
        registry.TryGet("cober", out var project);

        var key = Assert.Single(project!.ApiKeys);

        Assert.True(key.AllowedIps.Contains(IPAddress.Parse("203.0.113.10")));
        Assert.False(key.AllowedIps.Contains(IPAddress.Parse("198.51.100.7")));
    }

    [Fact]
    public void La_key_con_ips_propias_sobrescribe_las_del_proyecto()
    {
        var registry = Registry(TestProjects.Options(tweak: o => o.ApiKeys[0].AllowedIps = ["198.51.100.0/24"]));
        registry.TryGet("cober", out var project);

        var ips = project!.ApiKeys[0].AllowedIps;

        Assert.True(ips.Contains(IPAddress.Parse("198.51.100.7")));
        Assert.False(ips.Contains(IPAddress.Parse("203.0.113.10")));
    }

    [Fact]
    public void Sin_ips_en_ningun_nivel_nadie_puede_enviar()
    {
        var registry = Registry(TestProjects.Options(tweak: o => o.AllowedIps = []));
        registry.TryGet("cober", out var project);

        Assert.False(project!.ApiKeys[0].AllowedIps.Contains(IPAddress.Parse("203.0.113.10")));
    }

    [Fact]
    public void El_rate_limit_de_la_key_se_hereda_del_proyecto_si_no_define_uno()
    {
        var registry = Registry(TestProjects.Options(tweak: o =>
        {
            o.DefaultRateLimit = new RateLimitPolicyOptions { PermitLimit = 7, WindowSeconds = 30 };
            o.ApiKeys.Add(new ApiKeyOptions
            {
                Name = "propia",
                Key = "otra-key-0123456789-abcdefghijklmnopqrstu",
                RateLimit = new RateLimitPolicyOptions { PermitLimit = 3, WindowSeconds = 10 }
            });
        }));
        registry.TryGet("cober", out var project);

        Assert.Equal(7, project!.ApiKeys[0].RateLimit.PermitLimit);
        Assert.Equal(3, project.ApiKeys[1].RateLimit.PermitLimit);
    }

    [Fact]
    public void La_key_solo_se_guarda_como_hash()
    {
        var registry = Registry(TestProjects.Options());
        registry.TryGet("cober", out var project);

        var hash = project!.ApiKeys[0].KeyHash;

        Assert.Equal(32, hash.Length);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(TestProjects.API_KEY), hash);
    }

    [Fact]
    public void Una_key_vence_en_la_fecha_y_hora_indicada()
    {
        var expires = new DateTimeOffset(2026, 10, 9, 18, 0, 0, TimeSpan.FromHours(-3));
        var registry = Registry(TestProjects.Options(tweak: o => o.ApiKeys[0].ExpiresAt = expires));
        registry.TryGet("cober", out var project);
        var key = project!.ApiKeys[0];

        Assert.True(key.IsActive(expires.AddSeconds(-1)));
        Assert.False(key.IsActive(expires));
    }

    [Fact]
    public void Una_key_deshabilitada_no_esta_activa()
    {
        var registry = Registry(TestProjects.Options(tweak: o => o.ApiKeys[0].Enabled = false));
        registry.TryGet("cober", out var project);

        Assert.False(project!.ApiKeys[0].IsActive(DateTimeOffset.UtcNow));
    }
}
