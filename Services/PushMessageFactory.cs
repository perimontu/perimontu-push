using CoberPush.Api.Models;
using CoberPush.Api.Options;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Services;

/// <summary>Arma los mensajes FCM (notification + data + config Android + config APNs/iOS) a partir del contenido ya validado.</summary>
public sealed class PushMessageFactory(IOptions<PushOptions> options)
{
    private const string APNS_PRIORITY_HEADER = "apns-priority";
    private const string APNS_PRIORITY_IMMEDIATE = "10";
    private const string APNS_EXPIRATION_HEADER = "apns-expiration";

    private readonly PushOptions _options = options.Value;

    /// <param name="androidChannelId">Canal Android del proyecto; si es nulo o vacío se usa el global (<c>Push.AndroidChannelId</c>).</param>
    public Message CreateForToken(PushContent content, string token, string? androidChannelId = null)
    {
        // FirebaseAdmin 3.7 marca Token como obsoleto a favor de Fid (Firebase Installation ID), pero CoberApp
        // todavía obtiene un token de registro FCM clásico. Revisar cuando la app migre a FIDs.
#pragma warning disable CS0618
        return new Message
        {
            Token = token,
            Notification = BuildNotification(content),
            Data = BuildData(content),
            Android = BuildAndroid(content, androidChannelId),
            Apns = BuildApns(content)
        };
#pragma warning restore CS0618
    }

    public Message CreateForTopic(PushContent content, string topic, string? androidChannelId = null)
    {
        return new Message
        {
            Topic = topic,
            Notification = BuildNotification(content),
            Data = BuildData(content),
            Android = BuildAndroid(content, androidChannelId),
            Apns = BuildApns(content)
        };
    }

    private static Notification BuildNotification(PushContent content)
    {
        return new Notification { Title = content.Title, Body = content.Body, ImageUrl = content.ImageUrl };
    }

    /// <summary>Los datos libres van primero; las claves reservadas se escriben después y nunca pueden ser pisadas.</summary>
    private static Dictionary<string, string> BuildData(PushContent content)
    {
        var free = content.Data.Where(d => !PushDataKeys.RESERVED.Contains(d.Key));
        var data = new Dictionary<string, string>(free)
        {
            [PushDataKeys.MESSAGE_ID] = content.MessageId,
            [PushDataKeys.SENT_AT] = content.SentAt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [PushDataKeys.RECEIPT] = content.Receipt
        };

        if (!string.IsNullOrEmpty(content.Url))
        {
            data[PushDataKeys.URL] = content.Url;
        }

        return data;
    }

    private AndroidConfig BuildAndroid(PushContent content, string? androidChannelId)
    {
        return new AndroidConfig
        {
            Priority = Priority.High,
            TimeToLive = TimeSpan.FromSeconds(content.TtlSeconds),
            Notification = new AndroidNotification
            {
                ChannelId = string.IsNullOrWhiteSpace(androidChannelId) ? _options.AndroidChannelId : androidChannelId
            }
        };
    }

    /// <summary>
    /// iOS: el sonido no viene por defecto, hay que pedirlo en aps.sound. apns-priority 10 = entrega inmediata;
    /// apns-expiration (epoch en segundos; 0 = no reintentar) equivale al TTL de Android. FCM completa por su
    /// cuenta apns-push-type a partir del payload.
    /// </summary>
    private ApnsConfig BuildApns(PushContent content)
    {
        var expiration = content.TtlSeconds == 0
            ? 0
            : DateTimeOffset.UtcNow.AddSeconds(content.TtlSeconds).ToUnixTimeSeconds();

        return new ApnsConfig
        {
            Headers = new Dictionary<string, string>
            {
                [APNS_PRIORITY_HEADER] = APNS_PRIORITY_IMMEDIATE,
                [APNS_EXPIRATION_HEADER] = expiration.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            Aps = new Aps { Sound = _options.ApnsSound }
        };
    }
}
