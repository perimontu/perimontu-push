using CoberPush.Api.Models;
using CoberPush.Api.Options;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Services;

/// <summary>Arma los mensajes FCM (notification + data + config Android) a partir del contenido ya validado.</summary>
public sealed class PushMessageFactory(IOptions<PushOptions> options)
{
    private readonly PushOptions _options = options.Value;

    public Message CreateForToken(PushContent content, string token)
    {
        // FirebaseAdmin 3.7 marca Token como obsoleto a favor de Fid (Firebase Installation ID), pero CoberApp
        // todavía obtiene un token de registro FCM clásico. Revisar cuando la app migre a FIDs.
#pragma warning disable CS0618
        return new Message
        {
            Token = token,
            Notification = BuildNotification(content),
            Data = BuildData(content),
            Android = BuildAndroid(content)
        };
#pragma warning restore CS0618
    }

    public Message CreateForTopic(PushContent content, string topic)
    {
        return new Message
        {
            Topic = topic,
            Notification = BuildNotification(content),
            Data = BuildData(content),
            Android = BuildAndroid(content)
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

    private AndroidConfig BuildAndroid(PushContent content)
    {
        return new AndroidConfig
        {
            Priority = Priority.High,
            TimeToLive = TimeSpan.FromSeconds(content.TtlSeconds),
            Notification = new AndroidNotification { ChannelId = _options.AndroidChannelId }
        };
    }
}
