#nullable enable
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Crest.Media.Files;

/// <summary>
/// The built-in signer (docs/media.md › Serving and CDNs): an HMAC-SHA256 over the URL's path
/// and its expiry, carried as <c>exp</c> and <c>sig</c> query parameters. The byte endpoint
/// accepts a valid one as proof that the access gate already decided for this placement and
/// version, and nothing else as such proof. Without a CDN this is the private URL itself.
/// </summary>
public sealed class HmacMediaUrlSigner(byte[] key) : IMediaUrlSigner
{
    public const string ExpiresParameter = "exp";

    public const string SignatureParameter = "sig";

    public string Name => "hmac";

    public Uri Sign(Uri url, DateTimeOffset expires)
    {
        var (path, query) = Split(url);
        var seconds = expires.ToUnixTimeSeconds();
        var signature = Compute(path, seconds);
        var signed = QueryHelpers.AddQueryString(path + query, new Dictionary<string, string?>
        {
            [ExpiresParameter] = seconds.ToString(CultureInfo.InvariantCulture),
            [SignatureParameter] = signature,
        });

        return url.IsAbsoluteUri ? new Uri(url.GetLeftPart(UriPartial.Authority) + signed) : new Uri(signed, UriKind.Relative);
    }

    /// <summary>Whether <paramref name="signature"/> signs <paramref name="path"/> until
    /// <paramref name="expires"/> (unix seconds) and has not expired at <paramref name="now"/>.</summary>
    public bool Verify(string path, string? expires, string? signature, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(signature) ||
            !long.TryParse(expires, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < now.ToUnixTimeSeconds())
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Compute(path, seconds));
        var actual = Encoding.ASCII.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private string Compute(string path, long expires)
    {
        var payload = Encoding.UTF8.GetBytes($"{path}\n{expires.ToString(CultureInfo.InvariantCulture)}");
        return WebEncoders.Base64UrlEncode(HMACSHA256.HashData(key, payload));
    }

    private static (string Path, string Query) Split(Uri url)
    {
        var text = url.IsAbsoluteUri ? url.PathAndQuery : url.OriginalString;
        var index = text.IndexOf('?');
        return index < 0 ? (text, string.Empty) : (text[..index], text[index..]);
    }

    /// <summary>
    /// The tenant's signing key: the configured one (<c>Crest_Media:Files:UrlSigningKey</c>,
    /// base64, required when several servers serve the tenant without shared storage), else a
    /// random key kept in the tenant's file root, protected with data protection.
    /// </summary>
    public static byte[] ResolveKey(string? configured, string root, IDataProtectionProvider dataProtection)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Convert.FromBase64String(configured);
        }

        var protector = dataProtection.CreateProtector("Media.Files.UrlSigningKey");
        var path = Path.Combine(root, "url-signing.key");
        if (File.Exists(path))
        {
            return protector.Unprotect(File.ReadAllBytes(path));
        }

        var key = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(root);
        File.WriteAllBytes(path, protector.Protect(key));
        return key;
    }
}

// TODO (docs/media.md › Serving and CDNs): CDN signers, each configured per tenant like the
// storage and scanner providers. The private origin then requires the same token, and the
// CDN's cache key leaves it out.

/// <summary>Cloudflare token authentication (<c>is_timed_hmac_valid_v0</c>). Not built yet.</summary>
public sealed class CloudflareMediaUrlSigner : IMediaUrlSigner
{
    public string Name => "cloudflare";

    public Uri Sign(Uri url, DateTimeOffset expires) => throw new NotImplementedException("TODO: Cloudflare timed HMAC token.");
}

/// <summary>Amazon CloudFront signed URLs (RSA or ECDSA key pair). Not built yet.</summary>
public sealed class CloudFrontMediaUrlSigner : IMediaUrlSigner
{
    public string Name => "cloudfront";

    public Uri Sign(Uri url, DateTimeOffset expires) => throw new NotImplementedException("TODO: CloudFront signed URL with a canned policy.");
}

/// <summary>Bunny pull-zone token authentication (HMAC-SHA256 over path and expiry). Not built yet.</summary>
public sealed class BunnyMediaUrlSigner : IMediaUrlSigner
{
    public string Name => "bunny";

    public Uri Sign(Uri url, DateTimeOffset expires) => throw new NotImplementedException("TODO: Bunny token authentication.");
}
