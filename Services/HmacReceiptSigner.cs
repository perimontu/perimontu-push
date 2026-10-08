using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CoberPush.Api.Options;
using Microsoft.Extensions.Options;

namespace CoberPush.Api.Services;

public sealed class HmacReceiptSigner(IOptions<ReceiptOptions> options, TimeProvider clock) : IReceiptSigner
{
    private const int MAX_CLOCK_SKEW_SECONDS = 300;

    private readonly ReceiptOptions _options = options.Value;
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.HmacSecret);

    public string Sign(string messageId, long sentAtUnixSeconds)
    {
        var payload = Encoding.UTF8.GetBytes($"{messageId}.{sentAtUnixSeconds}");
        return Base64Url.EncodeToString(HMACSHA256.HashData(_key, payload));
    }

    public ReceiptVerification Verify(string messageId, long sentAtUnixSeconds, string receipt)
    {
        var expected = Encoding.UTF8.GetBytes(Sign(messageId, sentAtUnixSeconds));
        var received = Encoding.UTF8.GetBytes(receipt);

        // Primero la firma: así un tercero sin clave no puede sondear la vigencia.
        if (!CryptographicOperations.FixedTimeEquals(expected, received))
        {
            return ReceiptVerification.Invalid;
        }

        return CheckAge(sentAtUnixSeconds);
    }

    private ReceiptVerification CheckAge(long sentAtUnixSeconds)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();

        if (sentAtUnixSeconds > now + MAX_CLOCK_SKEW_SECONDS)
        {
            return ReceiptVerification.Invalid;
        }

        var maxAgeSeconds = TimeSpan.FromDays(_options.MaxAgeDays).TotalSeconds;
        return now - sentAtUnixSeconds > maxAgeSeconds ? ReceiptVerification.Expired : ReceiptVerification.Valid;
    }
}
