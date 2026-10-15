#nullable enable
using System.Globalization;
using Grove.Access;
using Grove.Media.Sharing;
using Grove.Sharing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Grove.Media.Files;

/// <summary>
/// File bytes by placement id (docs/media.md › Serving and CDNs, ruling F2):
/// <c>GET /files/{placementId}</c> serves the published version, <c>/files/{placementId}/v/{n}</c>
/// a given one. The access gate decides first; only clean versions are ever served. A public
/// file (an Anyone grant, from sharing's resolution) may be cached publicly; everything else
/// is <c>Cache-Control: private</c>, so no shared cache keeps it. A valid signed URL is
/// accepted as proof of an earlier decision for that path, and nothing else is.
/// </summary>
public static class FileServingEndpoint
{
    public const string Prefix = "/files";

    public static IEndpointRouteBuilder MapFileServing(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(Prefix + "/{placementId}", (HttpContext context, string placementId) => ServeAsync(context, placementId, null));
        routes.MapGet(Prefix + "/{placementId}/v/{version:int}", (HttpContext context, string placementId, int version) => ServeAsync(context, placementId, version));
        return routes;
    }

    /// <summary>The URL of a placement's bytes; <paramref name="version"/> null for the published one.</summary>
    public static string PathFor(string placementId, int? version) =>
        version is null ? $"{Prefix}/{Uri.EscapeDataString(placementId)}" : $"{Prefix}/{Uri.EscapeDataString(placementId)}/v/{version.Value.ToString(CultureInfo.InvariantCulture)}";

    public static async Task<IResult> ServeAsync(HttpContext context, string placementId, int? version)
    {
        var services = context.RequestServices;
        var cancellationToken = context.RequestAborted;
        var store = services.GetRequiredService<IMediaObjectStore>();
        var storage = services.GetRequiredService<IMediaStorageProvider>();

        var check = CheckSignature(context);
        if (check == MediaSignatureCheck.Invalid)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var signed = check == MediaSignatureCheck.Valid;

        var caller = services.GetRequiredService<ICallerContextAccessor>().Current;
        var placement = await store.GetPlacementAsync(placementId);
        if (placement?.Placement.LinkTargetPlacementId is { } target)
        {
            placement = await store.GetPlacementAsync(target);
        }

        if (placement is null)
        {
            return Results.NotFound();
        }

        // Without a signature, the decision: the service's read is the gated one.
        Stream? bytes = null;
        if (!signed)
        {
            if (caller is null)
            {
                return Results.NotFound();
            }

            bytes = await services.GetRequiredService<IMediaObjectService>().OpenReadAsync(placement.Placement.Id, version, cancellationToken);
            if (bytes is null)
            {
                return Results.NotFound();
            }
        }

        var content = await store.GetContentAsync(placement.Placement.ContentId);
        var number = version ?? content?.Content.PublishedVersion;
        var found = content?.Content.Versions.FirstOrDefault(candidate => candidate.Number == number);
        if (found is not { State: MediaVersionState.Clean })
        {
            bytes?.Dispose();
            return Results.NotFound();
        }

        bytes ??= await storage.OpenReadAsync(MediaStorageArea.Committed, found.BlobKey, cancellationToken);
        if (bytes is null)
        {
            return Results.NotFound();
        }

        var headers = context.Response.Headers;
        var isPublic = !signed && caller is not null && await IsPublicAsync(services, caller, placement, cancellationToken);
        headers.CacheControl = isPublic
            ? (version is null ? "public, max-age=300" : "public, max-age=31536000, immutable")
            : "private, no-store";
        headers.XContentTypeOptions = "nosniff";

        // User content never runs in the site's origin.
        headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src 'self' data:; media-src 'self'; style-src 'unsafe-inline'";

        var type = found.DetectedType ?? found.ClaimedType ?? "application/octet-stream";
        var inline = type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && !type.Contains("svg", StringComparison.OrdinalIgnoreCase) ||
            type.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
            type.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(placement.Placement.Name);
        headers.ContentDisposition = disposition.ToString();

        var etag = new EntityTagHeaderValue($"\"{found.Sha256}\"");
        return Results.Stream(bytes, type, entityTag: etag, enableRangeProcessing: true);
    }

    /// <summary>
    /// The request's signature, by any of the tenant's signers (the built-in one, and the private
    /// CDN zone's: the private origin requires the zone's own token, docs/media.md › Serving and
    /// CDNs). Checked against the path with the tenant's path base and the raw query.
    /// </summary>
    public static MediaSignatureCheck CheckSignature(HttpContext context)
    {
        var services = context.RequestServices;
        var signers = services.GetService<MediaUrlSigners>() ?? new MediaUrlSigners(services.GetRequiredService<HmacMediaUrlSigner>(), null);
        return signers.Check(context.Request.PathBase + context.Request.Path, context.Request.QueryString.Value ?? string.Empty, DateTimeOffset.UtcNow);
    }

    /// <summary>Public only when sharing says so (an Anyone grant); with the Sharing feature
    /// off nothing is known to be public, so everything is private.</summary>
    private static async Task<bool> IsPublicAsync(IServiceProvider services, CallerContext caller, MediaPlacementDocument placement, CancellationToken cancellationToken)
    {
        var resolver = services.GetService<ISharingResolver>();
        if (resolver is null)
        {
            return false;
        }

        var resolution = await resolver.ResolveAsync(caller, new ShareableRef(MediaFileShareableKind.KindName, placement.Placement.Id), cancellationToken);
        return resolution.IsPublic && !resolution.IsInactive;
    }
}
