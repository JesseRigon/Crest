using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Grove.Media.Scanning.Scanners;

/// <summary>
/// The HMAC both directions between Grove and an offsite scanner are signed with: SHA-256 over
/// <c>{timestamp}.{body}</c> with the provider's shared secret, sent as
/// <c>X-Scan-Signature: sha256={hex}</c> beside <c>X-Scan-Timestamp: {unix seconds}</c>.
/// The timestamp bounds replay.
/// </summary>
public static class ScanSignature
{
    public const string SignatureHeader = "X-Scan-Signature";
    public const string TimestampHeader = "X-Scan-Timestamp";
    public const string Scheme = "sha256=";

    public static string Sign(string secret, long timestamp, ReadOnlySpan<byte> body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        var prefix = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload.AsSpan(prefix.Length));

        return Scheme + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload));
    }

    public static bool Verify(string secret, string? timestampHeader, string? signatureHeader, ReadOnlySpan<byte> body, DateTimeOffset now, TimeSpan allowedSkew)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signatureHeader)
            || !long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
        {
            return false;
        }

        if (Math.Abs(now.ToUnixTimeSeconds() - timestamp) > allowedSkew.TotalSeconds)
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(secret, timestamp, body));
        var actual = Encoding.ASCII.GetBytes(signatureHeader.Trim().ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
