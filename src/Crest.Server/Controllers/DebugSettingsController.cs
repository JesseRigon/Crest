using Crest.DisplayManagement;
using Crest.Entities;
using Crest.Environment.Options;
using Crest.Services;
using Crest.Settings;
using Microsoft.AspNetCore.Mvc;
using CrestDebugSettings = Crest.AdminTheme.Api.CrestDebugSettings;

namespace Crest.Controllers;

/// <summary>Settings › Debugging: whether shape rendering writes its debug information.</summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/settings/debugging")]
public sealed class DebugSettingsController(IRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestDebugSettings>> GetAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, SettingsPermissions.ManageDebuggingSettings);
        if (access is null) return Forbid();

        var site = await access.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
        return Ok(new CrestDebugSettings(site.GetOrCreate<DebugSettings>().WriteShapeDebugInformation));
    }

    [HttpPut]
    public async Task<ActionResult<CrestDebugSettings>> PutAsync([FromBody] CrestDebugSettings update)
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

        return Ok(new CrestDebugSettings(update.WriteShapeDebugInformation));
    }
}
