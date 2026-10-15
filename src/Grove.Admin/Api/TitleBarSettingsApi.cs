#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Admin;

namespace Grove.Admin.Api;

public sealed class TitleBarSettingsApi : ApiContributor
{
    public override string Name => "TitleBarSettings";

    public override string FeatureId => "Grove.Admin";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("title-bar-settings");
        routes.MapGet("", ApiHandlers.Handle<TitleBarSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] TitleBarSettingsUpdate update) => ApiHandlers.For<TitleBarSettingsHandlers>(__http).PutAsync(update));
    }
}

public sealed class TitleBarSettingsHandlers(
    IAuthorizationService authorizationService,
    TitleBarSettingsStore store) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings)) return Forbid();
        return Ok(TitleBarSettingsDto.From(await store.GetAsync(HttpContext.RequestAborted)));
    }

    public async Task<IResult> PutAsync([FromBody] TitleBarSettingsUpdate update)
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings)) return Forbid();
        var saved = await store.SaveAsync(new TitleBarSettings
        {
            DisplayCultureLabel = update.DisplayCultureLabel,
            TenantAvatarImageUrl = update.TenantAvatarImageUrl,
            TenantAvatarShape = update.TenantAvatarShape,
            TenantAvatarClipPath = update.TenantAvatarClipPath,
            TenantAvatarBorderRadius = update.TenantAvatarBorderRadius,
        }, HttpContext.RequestAborted);
        return Ok(TitleBarSettingsDto.From(saved));
    }
}
