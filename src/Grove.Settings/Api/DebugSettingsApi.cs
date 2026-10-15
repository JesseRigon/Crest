#nullable enable

using Grove.DisplayManagement;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;

namespace Grove.Settings.Api;

public sealed class DebugSettingsApi : ApiContributor
{
    public override string Name => "DebugSettings";

    public override string FeatureId => "Grove.Settings";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/debugging");
        routes.MapGet("", ApiHandlers.Handle<DebugSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] DebugSettingsModel update) => ApiHandlers.For<DebugSettingsHandlers>(__http).PutAsync(update));
    }
}


/// <summary>Settings › Debugging: whether shape rendering writes its debug information.</summary>
public sealed class DebugSettingsHandlers(IRequestAccess requestAccess) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, SettingsPermissions.ManageDebuggingSettings);
        if (access is null) return Forbid();

        var site = await access.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
        return Ok(new DebugSettingsModel(site.GetOrCreate<DebugSettings>().WriteShapeDebugInformation));
    }

    public async Task<IResult> PutAsync([FromBody] DebugSettingsModel update)
    {
        var access = await requestAccess.AuthorizeAsync(User, SettingsPermissions.ManageDebuggingSettings);
        if (access is null) return Forbid();

        var siteService = access.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var changed = site.GetOrCreate<DebugSettings>().WriteShapeDebugInformation != update.WriteShapeDebugInformation;

        site.Alter<DebugSettings>(settings => settings.WriteShapeDebugInformation = update.WriteShapeDebugInformation);
        await siteService.UpdateSiteSettingsAsync(site);

        if (changed)
        {
            access.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<ShapeRenderingOptions>();
        }

        return Ok(new DebugSettingsModel(update.WriteShapeDebugInformation));
    }
}

public sealed record DebugSettingsModel(bool WriteShapeDebugInformation);
