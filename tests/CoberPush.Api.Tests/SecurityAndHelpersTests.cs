using System.Net;
using CoberPush.Api.Security;
using CoberPush.Api.Services;
using FirebaseAdmin.Messaging;

namespace CoberPush.Api.Tests;

public class IpAllowListTests
{
    private static IpAllowList Parse(params string[] entries)
    {
        Assert.True(IpAllowList.TryParse(entries, out var list, out var error), error);
        return list!;
    }

    [Fact]
    public void Ip_suelta_y_cidr_se_respetan()
    {
        var list = Parse("203.0.113.10", "198.51.100.0/24");

        Assert.True(list.Contains(IPAddress.Parse("203.0.113.10")));
        Assert.False(list.Contains(IPAddress.Parse("203.0.113.11")));
        Assert.True(list.Contains(IPAddress.Parse("198.51.100.77")));
        Assert.False(list.Contains(IPAddress.Parse("198.51.101.1")));
    }

    [Fact]
    public void Ipv6_y_ipv4_mapeada_funcionan()
    {
        var list = Parse("203.0.113.10", "2001:db8::1");

        Assert.True(list.Contains(IPAddress.Parse("2001:db8::1")));
        Assert.True(list.Contains(IPAddress.Parse("::ffff:203.0.113.10")));
        Assert.False(list.Contains(IPAddress.Parse("2001:db8::2")));
    }

    [Fact]
    public void Lista_vacia_o_ip_nula_no_permite_a_nadie()
    {
        Assert.False(Parse().Contains(IPAddress.Loopback));
        Assert.False(Parse("203.0.113.10").Contains(null));
    }

    [Theory]
    [InlineData("no-es-ip")]
    [InlineData("203.0.113.10/99")]
    [InlineData("")]
    public void Entradas_invalidas_se_informan(string entry)
    {
        Assert.False(IpAllowList.TryParse([entry], out var list, out var error));
        Assert.Null(list);
        Assert.Contains("IP/CIDR", error);
    }
}

public class TokenMaskerTests
{
    [Fact]
    public void Muestra_solo_los_ultimos_4_caracteres()
    {
        Assert.Equal("…ab12", TokenMasker.Mask("fcm-token-largo-ab12"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abcd")]
    public void Tokens_cortos_o_vacios_no_se_revelan(string? token)
    {
        Assert.Equal("…", TokenMasker.Mask(token));
    }
}

public class FcmErrorMapperTests
{
    [Theory]
    [InlineData(MessagingErrorCode.Unregistered, "UNREGISTERED", true)]
    [InlineData(MessagingErrorCode.SenderIdMismatch, "NO_EXISTE", false)]
    [InlineData(MessagingErrorCode.InvalidArgument, "INVALID_ARGUMENT", false)]
    [InlineData(MessagingErrorCode.QuotaExceeded, "QUOTA_EXCEEDED", false)]
    [InlineData(MessagingErrorCode.Unavailable, "UNAVAILABLE", false)]
    public void Traduce_codigos_y_marca_tokens_a_borrar(MessagingErrorCode code, string expected, bool remove)
    {
        Assert.Equal(expected, FcmErrorMapper.ToCode(code));
        Assert.Equal(remove, FcmErrorMapper.ShouldRemoveToken(code));
    }

    [Fact]
    public void Sin_codigo_es_desconocido_y_no_borra()
    {
        Assert.Equal("UNKNOWN", FcmErrorMapper.ToCode(null));
        Assert.False(FcmErrorMapper.ShouldRemoveToken(null));
    }
}
