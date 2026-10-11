#nullable enable
using Crest.Media.Files;

namespace Crest.Media.Library;

/// <summary>
/// The "Media library" shared drive (docs/media.md, ruling F1): the drive the existing
/// <c>/media</c> tree was imported into, where a media path is a chain of placement names from the
/// drive down. A path is only a way to find a placement; what is stored is the placement's id.
/// </summary>
public interface IMediaLibrary
{
    /// <summary>The library drive's placement id; null until the drive exists.</summary>
    Task<string?> GetDriveIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether <c>/media</c> paths are served from the drive: true once the import
    /// finished. Until then the media folder on disk serves them as before.</summary>
    Task<bool> IsActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The placement a library path names (path to folder chain to placement), or null. A soft
    /// link on the way is followed. This is a lookup, not a decision: the caller opens the
    /// placement through <see cref="IMediaObjectService"/>, which applies access, and a placement
    /// the caller may not view must be answered as not found.
    /// </summary>
    Task<string?> ResolvePathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>The library path of a placement (its names from the drive down), or null when it
    /// is not in the library drive.</summary>
    Task<string?> GetPathAsync(string placementId, CancellationToken cancellationToken = default);
}

/// <summary>A reference opened for the caller: the placement, its URL, and whether it is public.</summary>
/// <param name="Url">The URL to put in a page: <c>/files/{placementId}</c> under the tenant's
/// path base, signed when the file is private and signing was asked for.</param>
/// <param name="IsPublic">Shared with Anyone (sharing's resolution); with sharing off nothing is
/// known to be public.</param>
/// <param name="Path">The library path, when the reference was a path or
/// <see cref="MediaUrlOptions.IncludePath"/> asked for it and the placement is in the library drive.</param>
public sealed record ResolvedMedia(MediaItem Item, string Url, bool IsPublic, string? Path);

/// <summary>How a URL is made.</summary>
/// <param name="SignPrivate">Sign the URL of a private file (docs/media.md › Serving and CDNs):
/// true for pages a caller views; false for URLs handed to third parties that never carry the
/// caller (social images, feeds), which must stay plain and expire never.</param>
/// <param name="Version">A version to address instead of the published one.</param>
/// <param name="IncludePath">Also look up the placement's library path (an editor needs it;
/// a page does not).</param>
public sealed record MediaUrlOptions(bool SignPrivate = true, int? Version = null, bool IncludePath = false)
{
    public static readonly MediaUrlOptions Default = new();

    /// <summary>A plain URL even for private files.</summary>
    public static readonly MediaUrlOptions Unsigned = new(SignPrivate: false);
}

/// <summary>
/// Turns what fields, templates and shortcodes hold into placements and URLs (ruling F1). Ids are
/// preferred; library paths and <c>/media/...</c> URLs are accepted for compatibility and resolve
/// to their placement. Every placement is opened through <see cref="IMediaObjectService"/>, as the
/// current caller, so a file the caller may not view resolves to nothing. URLs are always
/// <c>/files/{placementId}</c> (docs/media.md, "Media URLs").
/// </summary>
public interface IMediaReferenceResolver
{
    Task<ResolvedMedia?> ResolveAsync(MediaReference reference, MediaUrlOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a value written in a template or markup: <c>/files/{placementId}</c>, a placement
    /// id, <c>/media/{path}</c>, or a library path; a query string is kept on the URL.
    /// </summary>
    Task<ResolvedMedia?> ResolveAsync(string value, MediaUrlOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The URL for a template value, query string kept. An absolute URL, a protocol-relative one or
    /// an app path (<c>~/</c>) is returned as it is; a value that resolves to no placement keeps
    /// its <c>/media/...</c> form, which answers 404 to whoever may not see it.
    /// </summary>
    Task<string> GetUrlAsync(string value, MediaUrlOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>The bytes of a reference's published version, after the access check.</summary>
    Task<Stream?> OpenReadAsync(MediaReference reference, CancellationToken cancellationToken = default);
}
