#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Microsoft.Extensions.Options;
using Grove.Media;

namespace Grove.Media.Api;

public sealed class MediaOptionsApi : ApiContributor
{
    public override string Name => "MediaOptions";

    public override string FeatureId => "Grove.Media";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("media/options");
        routes.MapGet("", ApiHandlers.Handle<MediaOptionsHandlers>(h => h.GetAsync()));
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
