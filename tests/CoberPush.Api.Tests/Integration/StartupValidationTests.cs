namespace CoberPush.Api.Tests.Integration;

/// <summary>La configuración es validada al arrancar: si falta un secreto, la API no debe iniciar.</summary>
public class StartupValidationTests
{
    [Fact]
    public void Sin_api_key_la_api_no_arranca()
    {
        using var factory = new ApiFactory(withApiKey: false);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Api_key_demasiado_corta_no_arranca()
    {
        using var factory = new ApiFactory(new() { ["Api:Key"] = "corta" });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Ip_invalida_en_la_lista_no_arranca()
    {
        using var factory = new ApiFactory(new() { ["Api:AllowedSendIps:0"] = "no-es-una-ip" });

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Con_configuracion_completa_arranca()
    {
        using var factory = new ApiFactory();

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }
}
