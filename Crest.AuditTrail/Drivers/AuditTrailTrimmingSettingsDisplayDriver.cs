using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.AuditTrail.Settings;
using Crest.AuditTrail.ViewModels;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;

namespace Crest.AuditTrail.Drivers;

public sealed class AuditTrailTrimmingSettingsDisplayDriver : SiteDisplayDriver<AuditTrailTrimmingSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public AuditTrailTrimmingSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => AuditTrailSettingsGroup.Id;

    public override async Task<IDisplayResult> EditAsync(ISite site, AuditTrailTrimmingSettings section, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, AuditTrailPermissions.ManageAuditTrailSettings))
        {
            return null;
        }

        return Initialize<AuditTrailTrimmingSettingsViewModel>("AuditTrailTrimmingSettings_Edit", model =>
        {
            model.RetentionDays = section.RetentionDays;
            model.LastRunUtc = section.LastRunUtc;
            model.Disabled = section.Disabled;
        }).Location("Content:10#Trimming;0")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AuditTrailTrimmingSettings section, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, AuditTrailPermissions.ManageAuditTrailSettings))
        {
            return null;
        }

        var model = new AuditTrailTrimmingSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        section.RetentionDays = model.RetentionDays;
        section.Disabled = model.Disabled;

        return await EditAsync(site, section, context);
    }
}
