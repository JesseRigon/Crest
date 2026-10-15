#nullable enable
using System.Globalization;
using Grove.Access;
using Grove.Environment.Cache;
using Grove.Environment.Shell;
using Grove.Media.Files;
using Grove.Media.Sharing;
using Grove.Modules;
using Grove.Access.Sharing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Media.Library;

/// <summary>Library settings, from <c>Grove_Media:Library</c>.</summary>
public sealed class MediaLibraryOptions
{
    /// <summary>How long a private file's signed URL is valid (docs/media.md › Serving and CDNs:
    /// the bound on how long a removed share can still be read). Minutes.</summary>
    public TimeSpan SignedUrlLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The import's default for existing files: false scans every one through the quarantine
    /// (they are served once clean); true records them clean as "imported, pre-existing" so the
    /// site keeps serving them while the scan runs. A recipe step can override it per run.
    /// </summary>
    public bool TrustExistingFiles { get; set; }
}

/// <summary>
/// Placements and URLs for what fields, templates and shortcodes hold (ruling F1). Every
/// placement is opened through <see cref="IMediaObjectService"/> as the current caller (an
/// anonymous visitor when there is none), so a reference the caller may not view resolves to
/// nothing. The URL is always <c>/files/{placementId}</c>; a public file's carries the CDN base,
/// a private one is signed when sharing has decided it is private (docs/media.md › Serving and
/// CDNs). With sharing off nothing is known to be public or private, so nothing is signed and
/// the byte endpoint decides on every request.
/// </summary>
public sealed class MediaReferenceResolver(
    IMediaLibrary library,
    IServiceProvider services,
    IHttpContextAccessor httpContextAccessor,
    ShellSettings shellSettings,
    IClock clock,
    IOptions<MediaOptions> mediaOptions,
    IOptions<MediaLibraryOptions> libraryOptions) : IMediaReferenceResolver
{
    private const string VersionSegment = "/v/";

    // Resolved on use, not injected: content handlers, shortcodes and Liquid filters take this
    // resolver, the content manager takes every content handler, and the object service takes
    // the access decision, whose permission providers and mappers reach the content manager (a
    // DI cycle the container cannot detect through factory registrations).
    private IMediaObjectService Files => services.GetRequiredService<IMediaObjectService>();

    public Task<ResolvedMedia?> ResolveAsync(MediaReference reference, MediaUrlOptions? options = null, CancellationToken cancellationToken = default) =>
        MediaCallers.AsCallerOrAnonymousAsync(services, () => ResolveCoreAsync(reference, options ?? MediaUrlOptions.Default, cancellationToken));

    public async Task<ResolvedMedia?> ResolveAsync(string value, MediaUrlOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var bare = value.Trim().RemoveQueryString(out var query);
        if (!TryParse(bare, out var reference, out var version))
        {
            return null;
        }

        options ??= MediaUrlOptions.Default;
        if (version is not null)
        {
            options = options with { Version = version };
        }

        var resolved = await ResolveAsync(reference, options, cancellationToken);
        return resolved is null || string.IsNullOrEmpty(query) ? resolved : resolved with { Url = AppendQuery(resolved.Url, query) };
    }

    public async Task<string> GetUrlAsync(string value, MediaUrlOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value) || IsExternal(value))
        {
            return value;
        }

        var resolved = await ResolveAsync(value, options, cancellationToken);
        if (resolved is not null)
        {
            return resolved.Url;
        }

        // Not a placement the caller may see: keep the media form, which resolves (and answers
        // 404) through the same access check when requested.
        var bare = value.Trim().RemoveQueryString(out var query);
        if (bare.StartsWith(FileServingEndpoint.Prefix + "/", StringComparison.OrdinalIgnoreCase) || bare.StartsWith(PathBase() + FileServingEndpoint.Prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var store = services.GetService<IMediaFileStore>();
        var path = MediaPathOf(bare) ?? bare;
        return store is null ? value : store.MapPathToPublicUrl(path) + query;
    }

    public async Task<Stream?> OpenReadAsync(MediaReference reference, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(reference, MediaUrlOptions.Unsigned, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        var placement = resolved.Item.Placement;
        return await MediaCallers.AsCallerOrAnonymousAsync(services, () => Files.OpenReadAsync(placement.LinkTargetPlacementId ?? placement.Id, null, cancellationToken));
    }

    private async Task<ResolvedMedia?> ResolveCoreAsync(MediaReference reference, MediaUrlOptions options, CancellationToken cancellationToken)
    {
        MediaItem? item = null;
        string? path = null;
        if (reference.HasPlacement)
        {
            item = await Files.GetAsync(reference.PlacementId!, cancellationToken);
        }

        if (item is null && !string.IsNullOrEmpty(reference.Path) && await library.ResolvePathAsync(reference.Path, cancellationToken) is { } placementId)
        {
            item = await Files.GetAsync(placementId, cancellationToken);
            path = item is null ? null : MediaLibraryPaths.Normalize(reference.Path);
        }

        if (item is null)
        {
            return null;
        }

        if (path is null && options.IncludePath)
        {
            path = await library.GetPathAsync(item.Placement.Id, cancellationToken);
        }

        var isPublic = await IsPublicAsync(item, cancellationToken);
        return new ResolvedMedia(item, await UrlForAsync(item, isPublic, options), isPublic, path);
    }

    /// <summary>Public when sharing resolves an Anyone grant on the placement (a soft link's target).</summary>
    private async Task<bool> IsPublicAsync(MediaItem item, CancellationToken cancellationToken)
    {
        var resolver = services.GetService<ISharingResolver>();
        var caller = services.GetService<ICallerContextAccessor>()?.Current;
        if (resolver is null || caller is null)
        {
            return false;
        }

        var target = new ShareableRef(MediaFileShareableKind.KindName, item.Placement.LinkTargetPlacementId ?? item.Placement.Id);
        var resolution = await resolver.ResolveAsync(caller, target, cancellationToken);
        return resolution.IsPublic && !resolution.IsInactive;
    }

    private Task<string> UrlForAsync(MediaItem item, bool isPublic, MediaUrlOptions options)
    {
        var url = PathBase() + FileServingEndpoint.PathFor(item.Placement.Id, options.Version);
        if (isPublic)
        {
            return Task.FromResult((mediaOptions.Value.CdnBaseUrl ?? string.Empty) + url);
        }

        // Signed only once sharing has decided the file is private; the signature stands for the
        // decision the gate just made, for minutes.
        // With a private CDN zone the URL is the zone's, signed with its token (docs/media.md ›
        // Serving and CDNs); otherwise Grove's own origin, HMAC-signed.
        if (options.SignPrivate && services.GetService<ISharingResolver>() is not null && services.GetServices<IMediaUrlSigner>().LastOrDefault() is { } signer)
        {
            var now = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));
            var lifetime = libraryOptions.Value.SignedUrlLifetime;
            var unsigned = signer.BaseUrl is { } zone ? new Uri(zone + url, UriKind.Absolute) : new Uri(url, UriKind.Relative);
            var signedUrl = signer.Sign(unsigned, now + lifetime);
            url = signedUrl.IsAbsoluteUri ? signedUrl.AbsoluteUri : signedUrl.OriginalString;
            KeepPrivate(now, lifetime);
        }

        return Task.FromResult(url);
    }

    /// <summary>
    /// A signed URL is this caller's proof of a decision, so what holds it must never reach
    /// anyone else: a cached shape or template block around it (dynamic cache) varies by user and
    /// expires while the URL still has half its life, and the page itself is marked private so
    /// no shared HTTP cache (a CDN, a proxy) keeps it.
    /// </summary>
    private void KeepPrivate(DateTimeOffset now, TimeSpan lifetime)
    {
        if (services.GetService<ICacheScopeManager>() is { } cacheScopes)
        {
            cacheScopes.AddContexts("user");
            cacheScopes.WithExpiryOn(now + lifetime / 2);
        }

        if (httpContextAccessor.HttpContext?.Response is { HasStarted: false } response)
        {
            response.Headers.CacheControl = "private, no-store";
        }
    }

    /// <summary>
    /// <c>/files/{id}[/v/{n}]</c> (with or without the tenant's path base), <c>/media/{path}</c>,
    /// a bare placement id, or a library path. An id-shaped value with no placement behind it is
    /// still tried as a path.
    /// </summary>
    internal bool TryParse(string value, out MediaReference reference, out int? version)
    {
        version = null;
        reference = default;
        var pathBase = PathBase();
        var files = value.StartsWith(pathBase + FileServingEndpoint.Prefix + "/", StringComparison.OrdinalIgnoreCase) && pathBase.Length > 0
            ? value[(pathBase.Length + FileServingEndpoint.Prefix.Length + 1)..]
            : value.StartsWith(FileServingEndpoint.Prefix + "/", StringComparison.OrdinalIgnoreCase)
                ? value[(FileServingEndpoint.Prefix.Length + 1)..]
                : null;

        if (files is not null)
        {
            var at = files.IndexOf(VersionSegment, StringComparison.Ordinal);
            if (at >= 0 && int.TryParse(files[(at + VersionSegment.Length)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                version = number;
                files = files[..at];
            }

            files = Uri.UnescapeDataString(files.Trim('/'));
            if (files.Length == 0 || files.Contains('/'))
            {
                return false;
            }

            reference = MediaReference.ForPlacement(files);
            return true;
        }

        if (MediaPathOf(value) is { } mediaPath)
        {
            reference = MediaReference.ForPath(mediaPath);
            return true;
        }

        reference = LooksLikeId(value) ? new MediaReference(value, value) : MediaReference.ForPath(value);
        return true;
    }

    /// <summary>The library path of a <c>/media/...</c> URL (with or without the path base or CDN), else null.</summary>
    private string? MediaPathOf(string value)
    {
        var options = mediaOptions.Value;
        var candidate = value;
        if (!string.IsNullOrEmpty(options.CdnBaseUrl) && candidate.StartsWith(options.CdnBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[options.CdnBaseUrl.Length..];
        }

        var requestPath = options.AssetsRequestPath.HasValue ? options.AssetsRequestPath.Value! : "/media";
        foreach (var prefix in new[] { PathBase() + requestPath + "/", requestPath + "/" })
        {
            if (prefix.Length > 1 && candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(candidate[prefix.Length..]);
            }
        }

        return null;
    }

    private string PathBase()
    {
        var pathBase = httpContextAccessor.HttpContext?.Request.PathBase;
        if (pathBase is { HasValue: true } known)
        {
            return known.Value!.TrimEnd('/');
        }

        return string.IsNullOrEmpty(shellSettings.RequestUrlPrefix) ? string.Empty : "/" + shellSettings.RequestUrlPrefix.Trim('/');
    }

    /// <summary>Ids are 26 lowercase Crockford base-32 characters (<see cref="IdGenerator"/>).</summary>
    internal static bool LooksLikeId(string value) =>
        value.Length == 26 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'z');

    private static bool IsExternal(string value) =>
        value.StartsWith("//", StringComparison.Ordinal) ||
        value.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("~/", StringComparison.Ordinal);

    private static string AppendQuery(string url, string query)
    {
        var parameters = QueryHelpers.ParseQuery(query);
        var pairs = parameters.SelectMany(parameter => parameter.Value.Select(item => new KeyValuePair<string, string?>(parameter.Key, item)));
        return QueryHelpers.AddQueryString(url, pairs);
    }
}
