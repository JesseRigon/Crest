using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.AuditTrail;
using Crest.AuditTrail.Settings;
using Crest.Contents.AuditTrail.Settings;
using Crest.Contents.AuditTrail.ViewModels;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;

namespace Crest.Contents.AuditTrail.Drivers;

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
            model.AllowedContentTypes = section.AllowedContentTypes;
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
        section.AllowedContentTypes = model.AllowedContentTypes;

        return await EditAsync(site, section, context);
    }
}
