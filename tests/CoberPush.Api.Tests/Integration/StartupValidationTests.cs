namespace CoberPush.Api.Tests.Integration;

/// <summary>La configuración es validada al arrancar: si algo de un proyecto está mal, la API no debe iniciar.</summary>
public class StartupValidationTests
{
    private static void AssertDoesNotStart(Dictionary<string, string?> overrides)
    {
        using var factory = new ApiFactory(overrides);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Con_configuracion_completa_arranca()
    {
        using var factory = new ApiFactory();

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void Sin_api_key_en_un_consumidor_no_arranca()
    {
        AssertDoesNotStart(new() { ["Projects:0:ApiKeys:0:Key"] = "" });
    }

    [Fact]
    public void Api_key_demasiado_corta_no_arranca()
    {
        AssertDoesNotStart(new() { ["Projects:0:ApiKeys:0:Key"] = "corta" });
    }

    [Fact]
    public void Ip_invalida_en_el_proyecto_no_arranca()
    {
        AssertDoesNotStart(new() { ["Projects:0:AllowedIps:0"] = "no-es-una-ip" });
    }

    [Fact]
    public void Ip_invalida_en_una_key_no_arranca()
    {
        AssertDoesNotStart(new() { ["Projects:0:ApiKeys:1:AllowedIps:0"] = "no-es-una-ip" });
    }

    [Fact]
    public void Secreto_de_recibos_corto_no_arranca()
    {
        AssertDoesNotStart(new() { ["Projects:1:Receipts:HmacSecret"] = "corto" });
    }

    [Fact]
    public void Id_de_proyecto_repetido_no_arranca()
    {
        AssertDoesNotStart(new() { ["Projects:1:Id"] = "COBER" });
    }

    [Fact]
    public void Proyecto_sin_variable_de_credencial_no_arranca()
    {
        AssertDoesNotStart(new() { ["Projects:0:Firebase:CredentialsEnvVar"] = "" });
    }

    [Fact]
    public void Sin_ningun_proyecto_habilitado_no_arranca()
    {
        AssertDoesNotStart(new()
        {
            ["Projects:0:Enabled"] = "false",
            ["Projects:1:Enabled"] = "false"
        });
    }

    [Fact]
    public void Proxy_de_confianza_invalido_no_arranca()
    {
        AssertDoesNotStart(new() { ["Api:KnownProxies:0"] = "no-es-ip" });
    }
}
