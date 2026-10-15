#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Microsoft.Extensions.Options;
using Grove.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Media.Api;

public sealed class MediaOptionsApi : ApiContributor
{
    public override string Name => "MediaOptions";

    public override string FeatureId => "Grove.Media";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("media/options");
        routes.MapGet("", ApiHandlers.Handle<MediaOptionsHandlers>(h => h.GetAsync()));
        routes.MapGet("cache", ApiHandlers.Handle<MediaCacheHandlers>(h => h.GetAsync()));
        routes.MapPost("cache/purge", ApiHandlers.Handle<MediaCacheHandlers>(h => h.PurgeAsync()));
    }
}

public sealed class MediaOptionsHandlers(IOptions<MediaOptions> options, IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, MediaPermissions.ViewMediaOptions)) return Forbid();
        var value = options.Value;
        return Ok(new MediaOptionsDto(value.SupportedSizes, value.AllowedFileExtensions.OrderBy(x => x), value.MaxBrowserCacheDays, value.MaxSecureFilesBrowserCacheDays, value.MaxCacheDays, value.MaxFileSize, value.MaxUploadChunkSize, value.CdnBaseUrl, value.AssetsRequestPath, value.AssetsPath, value.AssetsUsersFolder, value.UseTokenizedQueryString));
    }
}

/// <summary>
/// The asset cache (the Grove.Media.Cache feature) on the media options page: whether a remote
/// media store feeds it, and purging it. 404 while the feature is disabled.
/// </summary>
public sealed class MediaCacheHandlers(IAuthorizationService authorization, Grove.Environment.Shell.IShellFeaturesManager features, IServiceProvider services) : ApiHandlers
{
    public const string FeatureId = "Grove.Media.Cache";

    public async Task<IResult> GetAsync()
    {
        if (!await IsEnabledAsync()) return NotFound();
        if (!await authorization.AuthorizeAsync(User, MediaPermissions.ManageAssetCache)) return Forbid();

        // Not registered when the remote store is not configured.
        return Ok(new MediaCacheStatus(services.GetService<IMediaFileStoreCache>() is not null, null));
    }

    public async Task<IResult> PurgeAsync()
    {
        if (!await IsEnabledAsync()) return NotFound();
        if (!await authorization.AuthorizeAsync(User, MediaPermissions.ManageAssetCache)) return Forbid();

        if (services.GetService<IMediaFileStoreCache>() is not { } cache)
        {
            return BadRequest(new MediaCacheStatus(false, "The asset cache feature is enabled, but a remote media store feature is not enabled, or not configured with appsettings.json."));
        }

        var hasErrors = await cache.PurgeAsync();
        return Ok(new MediaCacheStatus(true, hasErrors ? "Asset cache purged, with errors." : "Asset cache purged."));
    }

    private async Task<bool> IsEnabledAsync() =>
        (await features.GetEnabledFeaturesAsync()).Any(feature => feature.Id == FeatureId);
}

/// <summary>Whether the asset cache is fed by a configured remote store, and the last action's message.</summary>
public sealed record MediaCacheStatus(bool IsConfigured, string? Message);
