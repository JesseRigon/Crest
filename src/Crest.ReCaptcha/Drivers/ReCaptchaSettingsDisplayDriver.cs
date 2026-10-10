using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Environment.Options;
using Crest.ReCaptcha.Configuration;
using Crest.ReCaptcha.ViewModels;
using Crest.Settings;

namespace Crest.ReCaptcha.Drivers;

public sealed class ReCaptchaSettingsDisplayDriver : SiteDisplayDriver<ReCaptchaSettings>
{
    public const string GroupId = "recaptcha";

    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ReCaptchaSettingsDisplayDriver(
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _optionsUpdateNotifier = optionsUpdateNotifier;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, ReCaptchaSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, ReCaptchaPermissions.ManageReCaptchaSettings))
        {
            return null;
        }

        return Initialize<ReCaptchaSettingsViewModel>("ReCaptchaSettings_Edit", model =>
        {
            model.SiteKey = settings.SiteKey;
            model.SecretKey = settings.SecretKey;
        }).Location("Content")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ReCaptchaSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, ReCaptchaPermissions.ManageReCaptchaSettings))
        {
            return null;
        }

        var model = new ReCaptchaSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var siteKey = model.SiteKey?.Trim();
        var secretKey = model.SecretKey?.Trim();

        if (settings.SiteKey != siteKey || settings.SecretKey != secretKey)
        {
            _optionsUpdateNotifier.RequestUpdate<ReCaptchaSettings>();
        }

        settings.SiteKey = siteKey;
        settings.SecretKey = secretKey;

        return await EditAsync(site, settings, context);
    }
}
