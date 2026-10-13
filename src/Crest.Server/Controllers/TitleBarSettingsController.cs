using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Admin;
using Crest.Services;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/title-bar-settings")]
public sealed class TitleBarSettingsController(
    IAuthorizationService authorizationService,
    TitleBarSettingsStore store) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TitleBarSettingsDto>> GetAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings)) return Forbid();
        return Ok(TitleBarSettingsDto.From(await store.GetAsync(HttpContext.RequestAborted)));
    }

    [HttpPut]
    public async Task<ActionResult<TitleBarSettingsDto>> PutAsync([FromBody] TitleBarSettingsUpdate update)
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
