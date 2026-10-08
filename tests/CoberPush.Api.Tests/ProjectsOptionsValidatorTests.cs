using CoberPush.Api.Options;
using CoberPush.Api.Projects;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Tests;

public class ProjectsOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(params ProjectOptions[] projects)
    {
        var all = new ProjectsOptions();
        all.Items.AddRange(projects);
        return new ProjectsOptionsValidator().Validate(null, all);
    }

    private static string FailureText(ValidateOptionsResult result)
    {
        Assert.True(result.Failed, "Se esperaba que la validación fallara.");
        return result.FailureMessage;
    }

    [Fact]
    public void Configuracion_completa_es_valida()
    {
        Assert.True(Validate(TestProjects.Options()).Succeeded);
    }

    [Fact]
    public void Dos_proyectos_distintos_son_validos()
    {
        var other = TestProjects.Options("bristol", o => o.ApiKeys[0].Key = "key-de-bristol-0123456789-abcdefghijklmn");

        Assert.True(Validate(TestProjects.Options(), other).Succeeded);
    }

    [Fact]
    public void Sin_proyectos_falla()
    {
        Assert.Contains("al menos un proyecto", FailureText(Validate()));
    }

    [Fact]
    public void Con_todos_deshabilitados_falla()
    {
        var text = FailureText(Validate(TestProjects.Options(tweak: o => o.Enabled = false)));

        Assert.Contains("habilitado", text);
    }

    [Theory]
    [InlineData("mi-proyecto")]
    [InlineData("con espacio")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData("12345678901234567890123456789012345")]
    public void Id_no_alfanumerico_o_demasiado_largo_falla(string id)
    {
        Assert.Contains(".Id:", FailureText(Validate(TestProjects.Options(id))));
    }

    [Fact]
    public void Ids_repetidos_sin_distinguir_mayusculas_fallan()
    {
        var a = TestProjects.Options("Cober");
        var b = TestProjects.Options("cober", o => o.ApiKeys[0].Key = "key-distinta-0123456789-abcdefghijklmnop");

        Assert.Contains("repetido", FailureText(Validate(a, b)));
    }

    [Fact]
    public void La_misma_key_en_dos_proyectos_falla_sin_mostrar_su_valor()
    {
        var text = FailureText(Validate(TestProjects.Options("uno"), TestProjects.Options("dos")));

        Assert.Contains("repetida", text);
        Assert.Contains("uno/backend", text);
        Assert.DoesNotContain(TestProjects.API_KEY, text);
    }

    [Fact]
    public void Nombres_de_key_repetidos_en_un_proyecto_fallan()
    {
        var project = TestProjects.Options(tweak: o => o.ApiKeys.Add(
            new ApiKeyOptions { Name = "Backend", Key = "otra-key-0123456789-abcdefghijklmnopqrstu" }));

        Assert.Contains("repetido", FailureText(Validate(project)));
    }

    [Fact]
    public void Key_corta_falla_y_no_se_muestra()
    {
        var text = FailureText(Validate(TestProjects.Options(tweak: o => o.ApiKeys[0].Key = "corta-123")));

        Assert.Contains("ApiKeys[0]", text);
        Assert.DoesNotContain("corta-123", text);
    }

    [Fact]
    public void Ip_invalida_a_nivel_proyecto_o_key_falla_indicando_donde()
    {
        var atProject = FailureText(Validate(TestProjects.Options(tweak: o => o.AllowedIps = ["no-es-ip"])));
        var atKey = FailureText(Validate(TestProjects.Options(tweak: o => o.ApiKeys[0].AllowedIps = ["300.1.1.1"])));

        Assert.Contains("AllowedIps", atProject);
        Assert.Contains("ApiKeys[0]", atKey);
    }

    [Fact]
    public void Faltas_de_firebase_hmac_y_url_se_informan_todas_juntas()
    {
        var text = FailureText(Validate(TestProjects.Options(tweak: o =>
        {
            o.Firebase.CredentialsEnvVar = "";
            o.Receipts.HmacSecret = "corto";
            o.UrlApi.BaseUrl = "no-es-url";
        })));

        Assert.Contains("CredentialsEnvVar", text);
        Assert.Contains("HmacSecret", text);
        Assert.Contains("BaseUrl", text);
    }

    [Fact]
    public void Rate_limit_fuera_de_rango_falla()
    {
        var project = TestProjects.Options(tweak: o => o.ApiKeys[0].RateLimit = new RateLimitPolicyOptions { PermitLimit = 0 });

        Assert.Contains("RateLimit", FailureText(Validate(project)));
    }

    [Fact]
    public void Bearer_ausente_falla()
    {
        var project = TestProjects.Options(tweak: o => o.UrlApi.BearerToken = "");

        Assert.Contains("BearerToken", FailureText(Validate(project)));
    }
}
