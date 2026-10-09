using CoberPush.Api.Models;
using CoberPush.Api.Options;
using CoberPush.Api.Services;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Tests;

public class PushMessageFactoryTests
{
    private readonly PushMessageFactory _factory = new(Opt.Of(new PushOptions()));

    private static PushContent Content(string? url = "https://www.cober.com.ar/app/x", Dictionary<string, string>? data = null)
    {
        return new PushContent(
            "12345", "Turno", "Mañana 10:00", url, "https://img.test/a.png", 1_791_500_000, "firma",
            data ?? new Dictionary<string, string> { ["tipo"] = "turno" }, 3600);
    }

    [Fact]
    public void Tokens_lleva_notification_data_y_android()
    {
        var message = _factory.CreateForToken(Content(), "t1");

#pragma warning disable CS0618 // Message.Token (ver PushMessageFactory)
        Assert.Equal("t1", message.Token);
#pragma warning restore CS0618

        Assert.Equal("Turno", message.Notification.Title);
        Assert.Equal("Mañana 10:00", message.Notification.Body);
        Assert.Equal("https://img.test/a.png", message.Notification.ImageUrl);
        Assert.Equal("cober_general", message.Android.Notification.ChannelId);
        Assert.Equal(FirebaseAdmin.Messaging.Priority.High, message.Android.Priority);
        Assert.Equal(TimeSpan.FromSeconds(3600), message.Android.TimeToLive);
    }

    [Fact]
    public void Data_incluye_contrato_con_la_app()
    {
        var message = _factory.CreateForToken(Content(), "t1");

        Assert.Equal("12345", message.Data["messageId"]);
        Assert.Equal("1791500000", message.Data["sentAt"]);
        Assert.Equal("firma", message.Data["receipt"]);
        Assert.Equal("https://www.cober.com.ar/app/x", message.Data["url"]);
        Assert.Equal("turno", message.Data["tipo"]);
    }

    [Fact]
    public void Sin_url_no_se_envia_la_clave_url()
    {
        var message = _factory.CreateForToken(Content(url: null), "t1");

        Assert.False(message.Data.ContainsKey("url"));
    }

    [Fact]
    public void Data_libre_no_puede_pisar_claves_reservadas()
    {
        var evil = new Dictionary<string, string>
        {
            ["url"] = "https://evil.com",
            ["messageId"] = "falso",
            ["RECEIPT"] = "falso",
            ["sentAt"] = "0"
        };

        var message = _factory.CreateForToken(Content(data: evil), "t1");

        Assert.Equal("https://www.cober.com.ar/app/x", message.Data["url"]);
        Assert.Equal("12345", message.Data["messageId"]);
        Assert.Equal("firma", message.Data["receipt"]);
        Assert.Equal("1791500000", message.Data["sentAt"]);
        Assert.DoesNotContain("RECEIPT", message.Data.Keys);
    }

    [Fact]
    public void Topic_arma_el_mismo_contenido()
    {
        var message = _factory.CreateForTopic(Content(), "novedades");

        Assert.Equal("novedades", message.Topic);
        Assert.Equal("12345", message.Data["messageId"]);
        Assert.Equal("cober_general", message.Android.Notification.ChannelId);
    }

    [Fact]
    public void Canal_de_android_es_configurable()
    {
        var factory = new PushMessageFactory(Opt.Of(new PushOptions { AndroidChannelId = "otro" }));

        Assert.Equal("otro", factory.CreateForTopic(Content(), "t").Android.Notification.ChannelId);
    }
}
