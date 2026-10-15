#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Media;
using Grove.Media.Processing;
using Grove.Media.Models;
using Grove.Media.Services;

namespace Grove.Media.Api;

public sealed class MediaProfilesApi : ApiContributor
{
    public override string Name => "MediaProfiles";

    public override string FeatureId => "Grove.Media";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("media/profiles");
        routes.MapGet("", ApiHandlers.Handle<MediaProfilesHandlers>(h => h.ListAsync()));
        routes.MapPut("{name}", (HttpContext __http, [FromRoute] string name, [FromBody] MediaProfileWriteRequest request) => ApiHandlers.For<MediaProfilesHandlers>(__http).SaveAsync(name, request));
        routes.MapDelete("{name}", (HttpContext __http, [FromRoute] string name) => ApiHandlers.For<MediaProfilesHandlers>(__http).DeleteAsync(name));
    }
}

public sealed class MediaProfilesHandlers(MediaProfilesManager manager, IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> ListAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        var document = await manager.GetMediaProfilesDocumentAsync();
        return Ok(document.MediaProfiles.OrderBy(profile => profile.Key, StringComparer.OrdinalIgnoreCase).Select(profile => MediaProfileDto.From(profile.Key, profile.Value)).ToArray());
    }

    public async Task<IResult> SaveAsync(string name, [FromBody] MediaProfileWriteRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("A profile name is required.");
        if (request.Width < 0 || request.Height < 0 || request.Quality is < 0 or > 100) return BadRequest("Dimensions must be positive and quality must be from 0 to 100.");
        var profile = new MediaProfile { Hint = request.Hint?.Trim(), Width = request.Width, Height = request.Height, Mode = request.Mode, Format = request.Format, Quality = request.Quality, BackgroundColor = request.BackgroundColor?.Trim(), AutoOrient = request.AutoOrient };
        await manager.UpdateMediaProfileAsync(name, profile);
        return Ok(MediaProfileDto.From(name, profile));
    }

    public async Task<IResult> DeleteAsync(string name)
    {
        if (!await CanManageAsync()) return Forbid();
        var document = await manager.GetMediaProfilesDocumentAsync();
        if (!document.MediaProfiles.ContainsKey(name)) return NotFound();
        await manager.RemoveMediaProfileAsync(name);
        return NoContent();
    }

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, MediaPermissions.ManageMediaProfiles);
}
