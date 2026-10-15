using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.AuditTrail;
using Grove.AuditTrail.Settings;
using Grove.Contents.AuditTrail.Settings;
using Grove.Contents.AuditTrail.ViewModels;
using Grove.DisplayManagement.Entities;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Settings;

namespace Grove.Contents.AuditTrail.Drivers;

public sealed class ContentAuditTrailSettingsDisplayDriver : SiteDisplayDriver<ContentAuditTrailSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ContentAuditTrailSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => AuditTrailSettingsGroup.Id;

    public override async Task<IDisplayResult> EditAsync(ISite site, ContentAuditTrailSettings section, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, AuditTrailPermissions.ManageAuditTrailSettings))
        {
            return null;
        }

        return Initialize<ContentAuditTrailSettingsViewModel>("ContentAuditTrailSettings_Edit", model =>
        {
            model.ExcludedContentTypes = section.ExcludedContentTypes;
        }).Location("Content:10#Content;5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ContentAuditTrailSettings section, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, AuditTrailPermissions.ManageAuditTrailSettings))
        {
            return null;
        }

        var model = new ContentAuditTrailSettings();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        section.ExcludedContentTypes = model.ExcludedContentTypes;

        return await EditAsync(site, section, context);
    }
}
