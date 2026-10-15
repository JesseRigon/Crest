namespace Grove.Media.Files;

/// <summary>Committed files, or uploads waiting for a verdict; kept apart (requirement 10).</summary>
public enum MediaStorageArea
{
    Quarantine,
    Committed,

    /// <summary>Derivatives of committed files made after an access decision: the private
    /// resize cache (ruling F2), keyed by placement, version and commands. Never served without
    /// the decision, and never part of a public cache.</summary>
    Derived,
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

    /// <summary>Deletes every key under <paramref name="prefix"/> (one key segment or more), such
    /// as a placement's derivatives when it is removed.</summary>
    Task DeletePrefixAsync(MediaStorageArea area, string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>A time-limited reference an offsite scanner can fetch directly (requirement 94);
    /// null when the provider cannot make one (the bytes are then streamed to the scanner).</summary>
    Task<Uri?> GetTemporaryReadUriAsync(MediaStorageArea area, string key, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

/// <summary>What a signer found on a request: no signature of its kind, a valid one, or one
/// that is wrong or expired.</summary>
public enum MediaSignatureCheck
{
    Absent,
    Valid,
    Invalid,
}

/// <summary>
/// Signs URLs for a zone (docs/media.md › Serving and CDNs): the access gate decides, then a
/// private file's URL is signed for minutes so the zone serves it to that caller only. The
/// built-in HMAC signer signs for Grove's own origin; the CDN signers (Cloudflare timed HMAC
/// token, CloudFront key pair, Bunny token) sign for the private CDN zone, and the private
/// origin requires the same token, so the CDN is never an open door.
/// </summary>
public interface IMediaUrlSigner
{
    string Name { get; }

    /// <summary>The zone's base URL (the private CDN hostname), or null for Grove's own origin.</summary>
    string? BaseUrl { get; }

    Uri Sign(Uri url, DateTimeOffset expires);

    /// <summary>Checks a request: <paramref name="path"/> with the tenant's path base,
    /// <paramref name="query"/> the raw query string (with its <c>?</c>, or empty).</summary>
    MediaSignatureCheck Verify(string path, string query, DateTimeOffset now);
}
