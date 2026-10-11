namespace Crest.Media.Files;

/// <summary>Committed files, or uploads waiting for a verdict; kept apart (requirement 10).</summary>
public enum MediaStorageArea
{
    Quarantine,
    Committed,
}

/// <summary>
/// Stores and serves bytes under opaque keys (object and version), never user-visible paths,
/// each tenant's bytes in that tenant's own root (docs/media.md § 3, requirements 10-11). Local
/// disk first; Azure Blob and S3 on the platform's storage libraries later.
/// </summary>
public interface IMediaStorageProvider
{
    string Name { get; }

    Task WriteAsync(MediaStorageArea area, string key, Stream content, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default);

    /// <summary>Moves a quarantined upload into committed storage on a clean verdict.</summary>
    Task CommitAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default);

    /// <summary>A time-limited reference an offsite scanner can fetch directly (requirement 94);
    /// null when the provider cannot make one (the bytes are then streamed to the scanner).</summary>
    Task<Uri?> GetTemporaryReadUriAsync(MediaStorageArea area, string key, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

/// <summary>
/// Signs URLs for a CDN zone (docs/media.md › Serving and CDNs): the access gate decides, then a
/// private file's URL is signed for minutes so the CDN serves it from cache to that caller only.
/// Providers: Cloudflare HMAC token, CloudFront key pair, Bunny token.
/// </summary>
public interface IMediaUrlSigner
{
    string Name { get; }

    Uri Sign(Uri url, DateTimeOffset expires);
}
