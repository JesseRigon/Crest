#nullable enable
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Grove.Media.Files;

/// <summary>
/// The private CDN zone (docs/media.md › Serving and CDNs), from the tenant's shell
/// configuration section <c>Grove_Media:Cdn</c>: the zone's base URL, which signer it checks and
/// that signer's secret. The CDN caches each file once (its cache key leaves the token out) and
/// fetches from Grove on a miss with the same token, which the private origin requires.
/// </summary>
public sealed class MediaCdnOptions
{
    public const string Section = "Grove_Media:Cdn";

    /// <summary>The private zone's base URL, such as <c>https://private.cdn.example.com</c>.</summary>
    public string? PrivateBaseUrl { get; set; }

    /// <summary><c>cloudflare</c>, <c>cloudfront</c> or <c>bunny</c>; empty for none (Grove's own
    /// origin, HMAC-signed).</summary>
    public string? Signer { get; set; }

    public CloudflareCdnOptions Cloudflare { get; set; } = new();

    public CloudFrontCdnOptions CloudFront { get; set; } = new();

    public BunnyCdnOptions Bunny { get; set; } = new();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PrivateBaseUrl) && !string.IsNullOrWhiteSpace(Signer);
}

public sealed class CloudflareCdnOptions
{
    /// <summary>The secret the WAF rule's <c>is_timed_hmac_valid_v0</c> checks with.</summary>
    public string? Secret { get; set; }

    /// <summary>The rule's time to live, in seconds: a token is valid this long after its
    /// timestamp. The signer back-dates the timestamp so the token ends at the URL's expiry.</summary>
    public int TtlSeconds { get; set; } = 900;

    /// <summary>The query parameter the rule reads (the separator is <c>?name=</c> or <c>&amp;name=</c>).</summary>
    public string Parameter { get; set; } = "verify";
}

public sealed class CloudFrontCdnOptions
{
    /// <summary>The public key's id in the distribution's trusted key group.</summary>
    public string? KeyPairId { get; set; }

    /// <summary>The private key, PEM (RSA 2048 or ECDSA P-256).</summary>
    public string? PrivateKey { get; set; }
}

public sealed class BunnyCdnOptions
{
    /// <summary>The pull zone's token authentication key.</summary>
    public string? SecurityKey { get; set; }
}

/// <summary>
/// Cloudflare token authentication: a WAF custom rule calls
/// <c>is_timed_hmac_valid_v0(secret, http.request.uri, ttl, http.request.timestamp.sec, 8)</c>.
/// The token is <c>{timestamp}-{base64(HMAC-SHA256(secret, message + timestamp))}</c>, the message
/// being the URI before the separator (<c>?verify=</c> or <c>&amp;verify=</c>, both 8 characters).
/// </summary>
public sealed class CloudflareMediaUrlSigner(string baseUrl, CloudflareCdnOptions options) : IMediaUrlSigner
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Secret ?? throw new InvalidOperationException("Cloudflare signing needs Grove_Media:Cdn:Cloudflare:Secret."));

    public string Name => "cloudflare";

    public string? BaseUrl { get; } = baseUrl.TrimEnd('/');

    public Uri Sign(Uri url, DateTimeOffset expires)
    {
        var text = url.IsAbsoluteUri ? url.PathAndQuery : url.OriginalString;
        var timestamp = expires.ToUnixTimeSeconds() - options.TtlSeconds;
        var token = $"{timestamp.ToString(CultureInfo.InvariantCulture)}-{Uri.EscapeDataString(Mac(text, timestamp))}";
        var signed = $"{text}{(text.Contains('?') ? '&' : '?')}{options.Parameter}={token}";
        return url.IsAbsoluteUri ? new Uri(url.GetLeftPart(UriPartial.Authority) + signed) : new Uri(signed, UriKind.Relative);
    }

    public MediaSignatureCheck Verify(string path, string query, DateTimeOffset now)
    {
        var uri = path + query;
        var at = FindParameter(uri, options.Parameter);
        if (at < 0)
        {
            return MediaSignatureCheck.Absent;
        }

        var message = uri[..at];
        var token = Uri.UnescapeDataString(uri[(at + options.Parameter.Length + 2)..].Split('&')[0]);
        var dash = token.IndexOf('-');
        if (dash <= 0 || !long.TryParse(token[..dash], NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) ||
            timestamp + options.TtlSeconds < now.ToUnixTimeSeconds())
        {
            return MediaSignatureCheck.Invalid;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Mac(message, timestamp)), Encoding.ASCII.GetBytes(token[(dash + 1)..]))
            ? MediaSignatureCheck.Valid
            : MediaSignatureCheck.Invalid;
    }

    private string Mac(string message, long timestamp) =>
        Convert.ToBase64String(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(message + timestamp.ToString(CultureInfo.InvariantCulture))));

    /// <summary>Where <c>?name=</c> or <c>&amp;name=</c> starts in <paramref name="uri"/>; -1 for none.</summary>
    internal static int FindParameter(string uri, string name)
    {
        foreach (var separator in new[] { '?', '&' })
        {
            var at = uri.IndexOf($"{separator}{name}=", StringComparison.Ordinal);
            if (at >= 0)
            {
                return at;
            }
        }

        return -1;
    }
}

/// <summary>
/// Amazon CloudFront signed URLs with a canned policy: the policy names the full URL and an end
/// time, signed with the key pair the distribution trusts (RSA: SHA-1; ECDSA P-256: SHA-256), and
/// carried as <c>Expires</c>, <c>Signature</c> and <c>Key-Pair-Id</c>. The origin checks the same
/// signature with the public half, against the URL as the zone received it.
/// </summary>
public sealed class CloudFrontMediaUrlSigner : IMediaUrlSigner, IDisposable
{
    public const string ExpiresParameter = "Expires";
    public const string SignatureParameter = "Signature";
    public const string KeyPairIdParameter = "Key-Pair-Id";

    private readonly string _keyPairId;
    private readonly RSA? _rsa;
    private readonly ECDsa? _ecdsa;

    public CloudFrontMediaUrlSigner(string baseUrl, CloudFrontCdnOptions options)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _keyPairId = options.KeyPairId ?? throw new InvalidOperationException("CloudFront signing needs Grove_Media:Cdn:CloudFront:KeyPairId.");
        var pem = options.PrivateKey ?? throw new InvalidOperationException("CloudFront signing needs Grove_Media:Cdn:CloudFront:PrivateKey.");
        if (pem.Contains("EC PRIVATE KEY", StringComparison.Ordinal))
        {
            _ecdsa = ECDsa.Create();
            _ecdsa.ImportFromPem(pem);
        }
        else
        {
            _rsa = RSA.Create();
            _rsa.ImportFromPem(pem);
        }
    }

    public string Name => "cloudfront";

    public string? BaseUrl { get; }

    public Uri Sign(Uri url, DateTimeOffset expires)
    {
        var absolute = url.IsAbsoluteUri ? url.AbsoluteUri : BaseUrl + url.OriginalString;
        var seconds = expires.ToUnixTimeSeconds();
        var signature = Encode(SignData(Encoding.UTF8.GetBytes(Policy(absolute, seconds))));
        return new Uri(QueryHelpers.AddQueryString(absolute, new Dictionary<string, string?>
        {
            [ExpiresParameter] = seconds.ToString(CultureInfo.InvariantCulture),
            [SignatureParameter] = signature,
            [KeyPairIdParameter] = _keyPairId,
        }));
    }

    public MediaSignatureCheck Verify(string path, string query, DateTimeOffset now)
    {
        var values = QueryHelpers.ParseQuery(query);
        if (!values.TryGetValue(SignatureParameter, out var signature))
        {
            return MediaSignatureCheck.Absent;
        }

        if (!long.TryParse(values.TryGetValue(ExpiresParameter, out var expires) ? expires.ToString() : null, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < now.ToUnixTimeSeconds() ||
            !string.Equals(values.TryGetValue(KeyPairIdParameter, out var keyPair) ? keyPair.ToString() : null, _keyPairId, StringComparison.Ordinal))
        {
            return MediaSignatureCheck.Invalid;
        }

        // The resource as signed: the zone's URL with every other parameter, in order.
        var rest = values
            .Where(pair => pair.Key is not (ExpiresParameter or SignatureParameter or KeyPairIdParameter))
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value)));
        var resource = QueryHelpers.AddQueryString(BaseUrl + path, rest);

        byte[] raw;
        try
        {
            raw = Decode(signature.ToString());
        }
        catch (FormatException)
        {
            return MediaSignatureCheck.Invalid;
        }

        var data = Encoding.UTF8.GetBytes(Policy(resource, seconds));
        var valid = _rsa is not null
            ? _rsa.VerifyData(data, raw, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1)
            : _ecdsa!.VerifyData(data, raw, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return valid ? MediaSignatureCheck.Valid : MediaSignatureCheck.Invalid;
    }

    public void Dispose()
    {
        _rsa?.Dispose();
        _ecdsa?.Dispose();
    }

    private byte[] SignData(byte[] data) => _rsa is not null
        ? _rsa.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1)
        : _ecdsa!.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    /// <summary>The canned policy, exactly as CloudFront rebuilds it: no white space.</summary>
    internal static string Policy(string resource, long expires) =>
        $"{{\"Statement\":[{{\"Resource\":\"{resource}\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{expires.ToString(CultureInfo.InvariantCulture)}}}}}}}]}}";

    /// <summary>CloudFront's URL-safe base64: <c>+</c> as <c>-</c>, <c>=</c> as <c>_</c>, <c>/</c> as <c>~</c>.</summary>
    internal static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('=', '_').Replace('/', '~');

    internal static byte[] Decode(string text) =>
        Convert.FromBase64String(text.Replace('-', '+').Replace('_', '=').Replace('~', '/'));
}

/// <summary>
/// Bunny pull-zone token authentication: <c>token</c> is the URL-safe base64 (no padding) of
/// SHA-256 over the security key, the URL path and the expiry, and <c>expires</c> the expiry in
/// unix seconds.
/// </summary>
public sealed class BunnyMediaUrlSigner(string baseUrl, BunnyCdnOptions options) : IMediaUrlSigner
{
    public const string TokenParameter = "token";
    public const string ExpiresParameter = "expires";

    private readonly string _key = options.SecurityKey ?? throw new InvalidOperationException("Bunny signing needs Grove_Media:Cdn:Bunny:SecurityKey.");

    public string Name => "bunny";

    public string? BaseUrl { get; } = baseUrl.TrimEnd('/');

    public Uri Sign(Uri url, DateTimeOffset expires)
    {
        var text = url.IsAbsoluteUri ? url.PathAndQuery : url.OriginalString;
        var at = text.IndexOf('?');
        var path = at < 0 ? text : text[..at];
        var seconds = expires.ToUnixTimeSeconds();
        var signed = QueryHelpers.AddQueryString(text, new Dictionary<string, string?>
        {
            [TokenParameter] = Token(path, seconds),
            [ExpiresParameter] = seconds.ToString(CultureInfo.InvariantCulture),
        });

        return url.IsAbsoluteUri ? new Uri(url.GetLeftPart(UriPartial.Authority) + signed) : new Uri(signed, UriKind.Relative);
    }

    public MediaSignatureCheck Verify(string path, string query, DateTimeOffset now)
    {
        var values = QueryHelpers.ParseQuery(query);
        if (!values.TryGetValue(TokenParameter, out var token))
        {
            return MediaSignatureCheck.Absent;
        }

        if (!long.TryParse(values.TryGetValue(ExpiresParameter, out var expires) ? expires.ToString() : null, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < now.ToUnixTimeSeconds())
        {
            return MediaSignatureCheck.Invalid;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Token(path, seconds)), Encoding.ASCII.GetBytes(token.ToString()))
            ? MediaSignatureCheck.Valid
            : MediaSignatureCheck.Invalid;
    }

    private string Token(string path, long expires) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(_key + path + expires.ToString(CultureInfo.InvariantCulture))))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

public static class CdnUrlSigners
{
    /// <summary>The configured private zone's signer, or null when none is configured.</summary>
    public static IMediaUrlSigner? Create(MediaCdnOptions options)
    {
        if (!options.IsConfigured)
        {
            return null;
        }

        return options.Signer!.Trim().ToLowerInvariant() switch
        {
            "cloudflare" => new CloudflareMediaUrlSigner(options.PrivateBaseUrl!, options.Cloudflare),
            "cloudfront" => new CloudFrontMediaUrlSigner(options.PrivateBaseUrl!, options.CloudFront),
            "bunny" => new BunnyMediaUrlSigner(options.PrivateBaseUrl!, options.Bunny),
            var other => throw new InvalidOperationException($"Unknown CDN signer '{other}' in {MediaCdnOptions.Section}:Signer (cloudflare, cloudfront or bunny)."),
        };
    }
}

/// <summary>
/// The tenant's signers: the built-in HMAC one for Grove's own origin, and the private CDN
/// zone's when configured. <see cref="Active"/> signs private URLs; a request is accepted when
/// any of them finds a valid signature and refused when one finds an invalid one.
/// </summary>
public sealed class MediaUrlSigners(HmacMediaUrlSigner hmac, IMediaUrlSigner? cdn)
{
    public HmacMediaUrlSigner Hmac { get; } = hmac;

    public IMediaUrlSigner? Cdn { get; } = cdn;

    public IMediaUrlSigner Active => Cdn ?? Hmac;

    public IEnumerable<IMediaUrlSigner> All => Cdn is null ? [Hmac] : [Cdn, Hmac];

    /// <summary>Valid when a signer accepts the request, invalid when one refuses it, absent
    /// when it carries no signature at all.</summary>
    public MediaSignatureCheck Check(string path, string query, DateTimeOffset now)
    {
        var result = MediaSignatureCheck.Absent;
        foreach (var signer in All)
        {
            switch (signer.Verify(path, query, now))
            {
                case MediaSignatureCheck.Valid:
                    return MediaSignatureCheck.Valid;
                case MediaSignatureCheck.Invalid:
                    result = MediaSignatureCheck.Invalid;
                    break;
            }
        }

        return result;
    }
}
