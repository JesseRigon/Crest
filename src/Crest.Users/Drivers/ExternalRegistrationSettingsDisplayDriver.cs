using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;
using Crest.Users.Models;

namespace Crest.Users.Drivers;

public sealed class ExternalRegistrationSettingsDisplayDriver : SiteDisplayDriver<ExternalRegistrationSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ExternalRegistrationSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => RegistrationSettingsDisplayDriver.GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, ExternalRegistrationSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, UsersPermissions.ManageUsers))
        {
            return null;
        }

        return Initialize<ExternalRegistrationSettings>("ExternalRegistrationSettings_Edit", model =>
        {
            model.DisableNewRegistrations = settings.DisableNewRegistrations;
            model.NoPassword = settings.NoPassword;
            model.NoUsername = settings.NoUsername;
            model.NoEmail = settings.NoEmail;
            model.UseScriptToGenerateUsername = settings.UseScriptToGenerateUsername;
            model.GenerateUsernameScript = settings.GenerateUsernameScript;
        }).Location("Content:5#External Authentication;5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ExternalRegistrationSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, UsersPermissions.ManageUsers))
        {
            return null;
        }

        await context.Updater.TryUpdateModelAsync(settings, Prefix);

        return await EditAsync(site, settings, context);
    }
}
