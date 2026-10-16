using System.Security.Cryptography;
using Grove.Data.Connectors;

namespace Grove.Data.Protocols.Webhooks;

/// <summary>
/// HMAC-SHA256 of the raw body with the shared secret, in the connection's signature header,
/// hex (optionally prefixed <c>sha256=</c>, as GitHub sends it). Compared in constant time.
/// </summary>
public sealed class HmacSha256HexScheme : IWebhookSignatureScheme
{
    public const string SchemeId = "hmac-sha256";
    public const string DefaultHeader = "X-Signature";

    public string Id => SchemeId;

    public bool Verify(ReadOnlySpan<byte> secret, WebhookRequest request, string? signatureHeader)
    {
        if (!HmacSignatures.TryHeader(request, signatureHeader ?? DefaultHeader, out var value))
        {
            return false;
        }

        if (value.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            value = value[7..];
        }

        byte[] given;
        try
        {
            given = Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return false;
        }

        return HmacSignatures.Matches(secret, request.Body.Span, given);
    }
}

/// <summary>HMAC-SHA256 of the raw body, base64 (Shopify, Slack-style variants without a timestamp).</summary>
public sealed class HmacSha256Base64Scheme : IWebhookSignatureScheme
{
    public const string SchemeId = "hmac-sha256-base64";

    public string Id => SchemeId;

    public bool Verify(ReadOnlySpan<byte> secret, WebhookRequest request, string? signatureHeader)
    {
        if (!HmacSignatures.TryHeader(request, signatureHeader ?? HmacSha256HexScheme.DefaultHeader, out var value))
        {
            return false;
        }

        byte[] given;
        try
        {
            given = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return false;
        }

        return HmacSignatures.Matches(secret, request.Body.Span, given);
    }
}

public static class HmacSignatures
{
    public static bool Matches(ReadOnlySpan<byte> secret, ReadOnlySpan<byte> body, ReadOnlySpan<byte> given)
    {
        Span<byte> expected = stackalloc byte[32];
        HMACSHA256.HashData(secret, body, expected);
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }

    public static bool TryHeader(WebhookRequest request, string name, out string value)
    {
        value = string.Empty;
        foreach (var (key, header) in request.Headers)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(header))
            {
                value = header.Trim();
                return true;
            }
        }

        return false;
    }
}
