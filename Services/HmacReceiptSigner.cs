using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CoberPush.Api.Projects;

namespace CoberPush.Api.Services;

public sealed class HmacReceiptSigner(TimeProvider clock) : IReceiptSigner
{
    private const int MAX_CLOCK_SKEW_SECONDS = 300;

    public string Sign(Project project, string messageId, long sentAtUnixSeconds)
    {
        var payload = Encoding.UTF8.GetBytes($"{project.Id}.{messageId}.{sentAtUnixSeconds}");
        return Base64Url.EncodeToString(HMACSHA256.HashData(project.ReceiptSecret, payload));
    }

    public ReceiptVerification Verify(Project project, string messageId, long sentAtUnixSeconds, string receipt)
    {
        var expected = Encoding.UTF8.GetBytes(Sign(project, messageId, sentAtUnixSeconds));
        var received = Encoding.UTF8.GetBytes(receipt);

        // Primero la firma: así un tercero sin clave no puede sondear la vigencia.
        if (!CryptographicOperations.FixedTimeEquals(expected, received))
        {
            return ReceiptVerification.Invalid;
        }

        return CheckAge(project, sentAtUnixSeconds);
    }

    private ReceiptVerification CheckAge(Project project, long sentAtUnixSeconds)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();

        if (sentAtUnixSeconds > now + MAX_CLOCK_SKEW_SECONDS)
        {
            return ReceiptVerification.Invalid;
        }

        var maxAgeSeconds = TimeSpan.FromDays(project.ReceiptMaxAgeDays).TotalSeconds;
        return now - sentAtUnixSeconds > maxAgeSeconds ? ReceiptVerification.Expired : ReceiptVerification.Valid;
    }
}
