#nullable enable
using System.Globalization;
using Grove.Access;
using Grove.Environment.Shell;
using Grove.Media.Files;
using Grove.Media.Middleware;
using Grove.Media.Models;
using Grove.Media.Processing;
using Grove.Media.Sharing;
using Grove.Access.Sharing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Media.Library;

/// <summary>
/// The <c>/media</c> request pipeline once the library is active (ruling F1), and image resizing
/// for file objects (ruling F2).
/// <list type="bullet">
/// <item><c>/media/{path}</c> resolves the path to its placement in the library drive (path, then
/// folder chain, then placement) and is served in place by the same access-checked serving as
/// <c>/files/{placementId}</c>: no redirect, so the URL keeps its form (requirement 9), costs no
/// round trip, and a page's cached HTML keeps working. A path that does not resolve, or a
/// placement the caller may not view, is a 404 — the media folder on disk is never served again.</item>
/// <item>Resize commands on either form run only after the decision. A public file (an Anyone
/// grant) is resized into the shared resize cache, keyed by placement, version and commands; a
/// private one is resized for this response only and never enters it.</item>
/// <item>Before the library is active, <c>/media</c> passes to the media folder's own pipeline.</item>
/// </list>
/// </summary>
internal sealed class MediaLibraryRequestMiddleware(
    IOptions<MediaOptions> mediaOptions,
    IImageProcessingEngine engine,
    IResizedImageCache cache,
    MediaCommandParser commandParser,
    SingleFlight<string, string> singleFlight,
    ShellSettings shellSettings,
    ILogger<MediaLibraryRequestMiddleware> logger) : IMiddleware
{
    private static readonly HashSet<string> s_resizable = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            await next(context);
            return;
        }

        if (request.Path.StartsWithSegments(mediaOptions.Value.AssetsRequestPath, StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            var library = context.RequestServices.GetRequiredService<IMediaLibrary>();
            if (!await library.IsActiveAsync(context.RequestAborted))
            {
                await next(context);
                return;
            }

            var placementId = await library.ResolvePathAsync(remaining.Value ?? string.Empty, context.RequestAborted);
            if (placementId is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await ServeAsync(context, placementId, null);
            return;
        }

        if (request.Path.StartsWithSegments(FileServingEndpoint.Prefix, StringComparison.OrdinalIgnoreCase, out var rest) &&
            TryParseFiles(rest.Value, out var id, out var version) &&
            commandParser.Parse(context) is { } commands)
        {
            await ResizeAsync(context, id, version, commands);
            return;
        }

        await next(context);
    }

    /// <summary><c>/{placementId}</c> or <c>/{placementId}/v/{n}</c>, as the byte endpoint routes them.</summary>
    internal static bool TryParseFiles(string? rest, out string placementId, out int? version)
    {
        placementId = string.Empty;
        version = null;
        var segments = (rest ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        switch (segments.Length)
        {
            case 1:
                placementId = segments[0];
                return true;
            case 3 when segments[1] == "v" && int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var number):
                placementId = segments[0];
                version = number;
                return true;
            default:
                return false;
        }
    }

    private async Task ServeAsync(HttpContext context, string placementId, int? version)
    {
        if (commandParser.Parse(context) is { } commands)
        {
            await ResizeAsync(context, placementId, version, commands);
            return;
        }

        await ServeOriginalAsync(context, placementId, version);
    }

    private static async Task ServeOriginalAsync(HttpContext context, string placementId, int? version)
    {
        var result = await FileServingEndpoint.ServeAsync(context, placementId, version);
        await result.ExecuteAsync(context);
    }

    private async Task ResizeAsync(HttpContext context, string placementId, int? version, MediaCommands commands)
    {
        var services = context.RequestServices;
        var cancellationToken = context.RequestAborted;
        var store = services.GetRequiredService<IMediaObjectStore>();

        // A signed URL stands for an earlier decision on exactly this path; nothing else does.
        var check = FileServingEndpoint.CheckSignature(context);
        if (check == MediaSignatureCheck.Invalid)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var signed = check == MediaSignatureCheck.Valid;

        var placement = await store.GetPlacementAsync(placementId);
        if (placement?.Placement.LinkTargetPlacementId is { } target)
        {
            placement = await store.GetPlacementAsync(target);
        }

        // The decision: the gated read, as the byte endpoint makes it.
        var caller = services.GetService<ICallerContextAccessor>()?.Current;
        if (placement is null || (!signed && (caller is null || await services.GetRequiredService<IMediaObjectService>().GetAsync(placement.Placement.Id, cancellationToken) is null)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var content = await store.GetContentAsync(placement.Placement.ContentId);
        var number = version ?? content?.Content.PublishedVersion;
        var found = content?.Content.Versions.FirstOrDefault(candidate => candidate.Number == number);
        if (found is not { State: MediaVersionState.Clean })
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Drafts are for those who may edit, as the byte endpoint has it.
        if (!signed && number != content!.Content.PublishedVersion)
        {
            await using var draft = await services.GetRequiredService<IMediaObjectService>().OpenReadAsync(placement.Placement.Id, number, cancellationToken);
            if (draft is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        var sourceType = found.DetectedType ?? found.ClaimedType ?? string.Empty;
        if (!s_resizable.Contains(sourceType))
        {
            await ServeOriginalAsync(context, placement.Placement.Id, version);
            return;
        }

        if (string.IsNullOrEmpty(commands.Format))
        {
            commands.Format = MediaImageProcessingMiddleware.ExtensionToFormat("." + sourceType["image/".Length..]);
        }

        var format = MediaResizingConstants.ParseFormat(commands.Format);
        var contentType = MediaResizingConstants.FormatToContentType(format);
        var extension = MediaResizingConstants.ContentTypeToExtension(contentType);
        var storage = services.GetRequiredService<IMediaStorageProvider>();
        var isPublic = !signed && caller is not null && await IsPublicAsync(services, caller, placement.Placement.Id, cancellationToken);

        if (!isPublic)
        {
            await ServePrivateAsync(context, storage, placement.Placement.Id, number!.Value, found.BlobKey, commands, format, contentType, cancellationToken);
            return;
        }

        // The public cache: keyed by the placement the bytes come from and the version, so a new
        // version is a new entry and nothing private is ever keyed into it.
        var key = ResizedImageCacheKey.Compute(
            shellSettings.Name,
            $"files/{placement.Placement.Id}/v{number!.Value.ToString(CultureInfo.InvariantCulture)}",
            commands.GetValues());
        var cacheControl = version is null
            ? "public, max-age=300"
            : $"public, max-age={TimeSpan.FromDays(mediaOptions.Value.MaxBrowserCacheDays).TotalSeconds.ToString(CultureInfo.InvariantCulture)}, immutable";

        var cached = await cache.GetAsync(key, extension, cancellationToken);
        if (cached is { } hit)
        {
            await WriteAsync(context, hit.Content, hit.ContentType, cacheControl);
            return;
        }

        var produced = await singleFlight.ScheduleAsync(key, async _ =>
        {
            var existing = await cache.GetAsync(key, extension, CancellationToken.None);
            if (existing is { } again)
            {
                again.Content.Dispose();
                return again.ContentType;
            }

            try
            {
                await using var source = await storage.OpenReadAsync(MediaStorageArea.Committed, found.BlobKey, CancellationToken.None);
                if (source is null)
                {
                    return null;
                }

                using var result = await engine.ProcessAsync(source, MediaImageProcessingMiddleware.BuildEngineCommands(commands, format), CancellationToken.None);
                result.Output.Position = 0;
                await cache.SetAsync(key, result.Output, result.ContentType, TimeSpan.FromDays(mediaOptions.Value.MaxCacheDays), CancellationToken.None);
                return result.ContentType;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error resizing placement '{PlacementId}'.", placement.Placement.Id);
                return null;
            }
        });

        if (produced is null || await cache.GetAsync(key, extension, cancellationToken) is not { } entry)
        {
            await ServeOriginalAsync(context, placement.Placement.Id, version);
            return;
        }

        await WriteAsync(context, entry.Content, entry.ContentType, cacheControl);
    }

    /// <summary>
    /// The private resize cache (ruling F2): derivatives of a private file, made only after the
    /// decision, kept in the storage provider's <see cref="MediaStorageArea.Derived"/> area keyed
    /// by placement, version and commands, and served <c>private, no-store</c>. Nothing here ever
    /// enters the shared resize cache. A placement's derivatives go when it is removed; a new
    /// version is a new key.
    /// </summary>
    private async Task ServePrivateAsync(HttpContext context, IMediaStorageProvider storage, string placementId, int version, string blobKey, MediaCommands commands, Format format, string contentType, CancellationToken cancellationToken)
    {
        var key = PrivateCacheKey(placementId, version, commands.GetValues());
        var cached = await storage.OpenReadAsync(MediaStorageArea.Derived, key, cancellationToken);
        if (cached is null)
        {
            var produced = await singleFlight.ScheduleAsync("private:" + key, async _ =>
            {
                if (await storage.OpenReadAsync(MediaStorageArea.Derived, key, CancellationToken.None) is { } again)
                {
                    await again.DisposeAsync();
                    return key;
                }

                try
                {
                    await using var source = await storage.OpenReadAsync(MediaStorageArea.Committed, blobKey, CancellationToken.None);
                    if (source is null)
                    {
                        return null;
                    }

                    using var result = await engine.ProcessAsync(source, MediaImageProcessingMiddleware.BuildEngineCommands(commands, format), CancellationToken.None);
                    result.Output.Position = 0;
                    await storage.WriteAsync(MediaStorageArea.Derived, key, result.Output, CancellationToken.None);
                    return key;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Error resizing private placement '{PlacementId}'.", placementId);
                    return null;
                }
            });

            cached = produced is null ? null : await storage.OpenReadAsync(MediaStorageArea.Derived, key, cancellationToken);
        }

        if (cached is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await WriteAsync(context, cached, contentType, "private, no-store");
    }

    /// <summary><c>{placementId}/v{n}/{hash of the commands}</c>: an opaque storage key.</summary>
    internal static string PrivateCacheKey(string placementId, int version, IEnumerable<KeyValuePair<string, string?>> commands)
    {
        var canonical = string.Join('&', commands
            .Where(pair => pair.Value is not null)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)))[..32];
        return $"{placementId}/v{version.ToString(CultureInfo.InvariantCulture)}/{hash}";
    }

    private static async Task<bool> IsPublicAsync(IServiceProvider services, CallerContext caller, string placementId, CancellationToken cancellationToken)
    {
        if (services.GetService<ISharingResolver>() is not { } resolver)
        {
            return false;
        }

        var resolution = await resolver.ResolveAsync(caller, new ShareableRef(MediaFileShareableKind.KindName, placementId), cancellationToken);
        return resolution.IsPublic && !resolution.IsInactive;
    }

    private static async Task WriteAsync(HttpContext context, Stream content, string contentType, string cacheControl)
    {
        await using (content)
        {
            var response = context.Response;
            response.ContentType = contentType;
            response.Headers.CacheControl = cacheControl;
            response.Headers.XContentTypeOptions = "nosniff";
            if (content.CanSeek)
            {
                response.ContentLength = content.Length - content.Position;
            }

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await content.CopyToAsync(response.Body, context.RequestAborted);
            }
        }
    }
}
