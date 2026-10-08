using CoberPush.Api.Services;

namespace CoberPush.Api.Tests;

public class HmacReceiptSignerTests
{
    private readonly FakeClock _clock = new();
    private readonly CoberPush.Api.Projects.Project _project = TestProjects.Create();

    private long Now => _clock.GetUtcNow().ToUnixTimeSeconds();

    [Fact]
    public void Firma_es_determinista_y_url_safe()
    {
        var signer = TestServices.CreateSigner(_clock);

        var a = signer.Sign(_project, "12345", Now);
        var b = signer.Sign(_project, "12345", Now);

        Assert.Equal(a, b);
        Assert.Matches("^[A-Za-z0-9_-]+$", a);
    }

    [Fact]
    public void Firma_valida_se_acepta()
    {
        var signer = TestServices.CreateSigner(_clock);
        var receipt = signer.Sign(_project, "12345", Now);

        Assert.Equal(ReceiptVerification.Valid, signer.Verify(_project, "12345", Now, receipt));
    }

    [Fact]
    public void Cambiar_messageId_o_fecha_invalida_la_firma()
    {
        var signer = TestServices.CreateSigner(_clock);
        var receipt = signer.Sign(_project, "12345", Now);

        Assert.Equal(ReceiptVerification.Invalid, signer.Verify(_project, "99999", Now, receipt));
        Assert.Equal(ReceiptVerification.Invalid, signer.Verify(_project, "12345", Now - 1, receipt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Firma_ajena_o_malformada_es_invalida(string receipt)
    {
        var signer = TestServices.CreateSigner(_clock);

        Assert.Equal(ReceiptVerification.Invalid, signer.Verify(_project, "12345", Now, receipt));
    }

    [Fact]
    public void Firma_con_otro_secreto_es_invalida()
    {
        var other = TestProjects.Create(tweak: o => o.Receipts.HmacSecret = "otro-secreto-0123456789-abcdefghijklmnop");
        var signer = TestServices.CreateSigner(_clock);

        Assert.Equal(ReceiptVerification.Invalid, signer.Verify(_project, "12345", Now, signer.Sign(other, "12345", Now)));
    }

    [Fact]
    public void Un_recibo_de_un_proyecto_no_vale_en_otro_aunque_compartan_secreto()
    {
        var other = TestProjects.Create("bristol");
        var signer = TestServices.CreateSigner(_clock);

        var receipt = signer.Sign(_project, "12345", Now);

        Assert.Equal(ReceiptVerification.Invalid, signer.Verify(other, "12345", Now, receipt));
    }

    [Fact]
    public void Dentro_de_30_dias_es_valida_y_pasado_el_limite_expira()
    {
        var signer = TestServices.CreateSigner(_clock);
        var sentAt = Now;
        var receipt = signer.Sign(_project, "12345", sentAt);

        _clock.Advance(TimeSpan.FromDays(29));
        Assert.Equal(ReceiptVerification.Valid, signer.Verify(_project, "12345", sentAt, receipt));

        _clock.Advance(TimeSpan.FromDays(2));
        Assert.Equal(ReceiptVerification.Expired, signer.Verify(_project, "12345", sentAt, receipt));
    }

    [Fact]
    public void La_vigencia_sale_del_proyecto()
    {
        var shortLived = TestProjects.Create(tweak: o => o.Receipts.MaxAgeDays = 1);
        var signer = TestServices.CreateSigner(_clock);
        var sentAt = Now;
        var receipt = signer.Sign(shortLived, "12345", sentAt);

        _clock.Advance(TimeSpan.FromDays(2));

        Assert.Equal(ReceiptVerification.Expired, signer.Verify(shortLived, "12345", sentAt, receipt));
    }

    [Fact]
    public void Fecha_de_envio_en_el_futuro_es_invalida()
    {
        var signer = TestServices.CreateSigner(_clock);
        var future = Now + 3600;

        Assert.Equal(ReceiptVerification.Invalid, signer.Verify(_project, "12345", future, signer.Sign(_project, "12345", future)));
    }

    [Fact]
    public void Un_recibo_vencido_con_firma_falsa_no_revela_la_vigencia()
    {
        var signer = TestServices.CreateSigner(_clock);
        _clock.Advance(TimeSpan.FromDays(60));

        Assert.Equal(ReceiptVerification.Invalid, signer.Verify(_project, "12345", Now - 90 * 86400, "firma-falsa"));
    }
}
