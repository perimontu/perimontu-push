using CoberPush.Api.Models;
using CoberPush.Api.Options;
using CoberPush.Api.Services;
using CoberPush.Api.Validation;

namespace CoberPush.Api.Tests;

public class PushRequestValidatorTests
{
    private readonly FakeClock _clock = new();
    private readonly PushRequestValidator _validator;

    public PushRequestValidatorTests()
    {
        _validator = TestServices.CreateValidator(_clock);
    }

    private static SendPushRequest Valid() => new()
    {
        Tokens = ["token-1"],
        Title = "Turno confirmado",
        Body = "Tu turno es mañana",
        Url = "https://cober.com.ar/app/pwa/central_de_turnos"
    };

    [Fact]
    public void Pedido_valido_genera_contenido_firmado_con_defaults()
    {
        var result = _validator.BuildContent(Valid());

        Assert.True(result.IsValid);
        var content = result.Content!;
        Assert.Equal("https://www.cober.com.ar/app/pwa/central_de_turnos", content.Url);
        Assert.Equal(86_400, content.TtlSeconds);
        Assert.Equal(_clock.GetUtcNow().ToUnixTimeSeconds(), content.SentAt);
        Assert.Equal(32, content.MessageId.Length); // GUID "N"
        Assert.Equal(
            ReceiptVerification.Valid,
            TestServices.CreateSigner(_clock).Verify(content.MessageId, content.SentAt, content.Receipt));
    }

    [Fact]
    public void Respeta_el_messageId_del_llamador()
    {
        var result = _validator.BuildContent(Valid() with { MessageId = "pedido:12345" });

        Assert.Equal("pedido:12345", result.Content!.MessageId);
    }

    [Theory]
    [InlineData("tiene espacios")]
    [InlineData("con/barra")]
    public void MessageId_invalido_se_rechaza(string id)
    {
        var result = _validator.BuildContent(Valid() with { MessageId = id });

        Assert.Contains("messageId", result.Errors.Keys);
    }

    [Fact]
    public void MessageId_de_mas_de_64_caracteres_se_rechaza()
    {
        var result = _validator.BuildContent(Valid() with { MessageId = new string('a', 65) });

        Assert.Contains("messageId", result.Errors.Keys);
    }

    [Fact]
    public void Titulo_y_cuerpo_son_obligatorios()
    {
        var result = _validator.BuildContent(Valid() with { Title = " ", Body = null });

        Assert.Contains("title", result.Errors.Keys);
        Assert.Contains("body", result.Errors.Keys);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Url_fuera_de_la_politica_se_rechaza()
    {
        var result = _validator.BuildContent(Valid() with { Url = "https://evil.com/app" });

        Assert.Contains("url", result.Errors.Keys);
    }

    [Fact]
    public void Sin_url_es_valido()
    {
        var result = _validator.BuildContent(Valid() with { Url = null });

        Assert.True(result.IsValid);
        Assert.Null(result.Content!.Url);
    }

    [Fact]
    public void Imagen_debe_ser_https()
    {
        Assert.Contains("imageUrl", _validator.BuildContent(Valid() with { ImageUrl = "http://x.com/a.png" }).Errors.Keys);
        Assert.True(_validator.BuildContent(Valid() with { ImageUrl = "https://x.com/a.png" }).IsValid);
    }

    [Theory]
    [InlineData("url")]
    [InlineData("messageId")]
    [InlineData("receipt")]
    [InlineData("sentAt")]
    [InlineData("URL")]
    [InlineData("google.x")]
    [InlineData("gcm_algo")]
    [InlineData("from")]
    [InlineData("con espacio")]
    public void Claves_de_data_reservadas_o_invalidas_se_rechazan(string key)
    {
        var result = _validator.BuildContent(Valid() with { Data = new() { [key] = "x" } });

        Assert.Contains("data", result.Errors.Keys);
    }

    [Fact]
    public void Data_con_demasiadas_entradas_o_valores_largos_se_rechaza()
    {
        var many = Enumerable.Range(0, 21).ToDictionary(i => $"k{i}", _ => "v");
        Assert.Contains("data", _validator.BuildContent(Valid() with { Data = many }).Errors.Keys);

        var longValue = new Dictionary<string, string> { ["k"] = new string('x', 501) };
        Assert.Contains("data", _validator.BuildContent(Valid() with { Data = longValue }).Errors.Keys);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2_419_201)]
    public void Ttl_fuera_de_rango_se_rechaza(int ttl)
    {
        Assert.Contains("ttlSeconds", _validator.BuildContent(Valid() with { TtlSeconds = ttl }).Errors.Keys);
    }

    [Fact]
    public void Ttl_cero_es_valido()
    {
        Assert.Equal(0, _validator.BuildContent(Valid() with { TtlSeconds = 0 }).Content!.TtlSeconds);
    }

    [Fact]
    public void Payload_demasiado_grande_se_rechaza()
    {
        var big = Enumerable.Range(0, 10).ToDictionary(i => $"k{i}", _ => new string('x', 480));

        var result = _validator.BuildContent(Valid() with { Data = big });

        Assert.Contains("payload", result.Errors.Keys);
    }

    [Fact]
    public void Tokens_se_validan()
    {
        Assert.Empty(_validator.ValidateTokens(["a", "b"]));
        Assert.Contains("tokens", _validator.ValidateTokens(null).Keys);
        Assert.Contains("tokens", _validator.ValidateTokens([]).Keys);
        Assert.Contains("tokens", _validator.ValidateTokens(["con espacio"]).Keys);
        Assert.Contains("tokens", _validator.ValidateTokens([""]).Keys);
    }

    [Fact]
    public void Maximo_de_tokens_es_configurable()
    {
        var validator = TestServices.CreateValidator(_clock, new PushOptions { MaxTokensPerRequest = 2 });

        Assert.Empty(validator.ValidateTokens(["a", "b"]));
        Assert.Contains("tokens", validator.ValidateTokens(["a", "b", "c"]).Keys);
    }

    [Theory]
    [InlineData("novedades", true)]
    [InlineData("grupo-1_a.b~c%", true)]
    [InlineData("con espacio", false)]
    [InlineData("/topics/x", false)]
    [InlineData("", false)]
    public void Topic_se_valida(string topic, bool ok)
    {
        Assert.Equal(ok, _validator.ValidateTopic(topic).Count == 0);
    }
}
