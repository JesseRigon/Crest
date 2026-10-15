using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.DisplayManagement.Entities;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Environment.Shell;
using Grove.OpenApi.Settings;
using Grove.OpenApi.ViewModels;
using Grove.Settings;

namespace Grove.OpenApi.Drivers;

public sealed class OpenApiSettingsDisplayDriver : SiteDisplayDriver<OpenApiSettings>
{
    public const string GroupId = "openapi";

    private readonly IEnumerable<IOpenApiUIFeature> _uiFeatures;
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public OpenApiSettingsDisplayDriver(
        IEnumerable<IOpenApiUIFeature> uiFeatures,
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _uiFeatures = uiFeatures;
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, OpenApiSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, OpenApiPermissions.ManageOpenApi))
        {
            return null;
        }

        context.AddTenantReloadWarningWrapper();

        return Initialize<OpenApiSettingsViewModel>("OpenApiSettings_Edit", model =>
        {
            model.IsSwaggerUIEnabled = _uiFeatures.Any(f => f is SwaggerUIFeature);
            model.IsReDocUIEnabled = _uiFeatures.Any(f => f is ReDocUIFeature);
            model.IsScalarUIEnabled = _uiFeatures.Any(f => f is ScalarUIFeature);
            model.AllowAnonymousSchemaAccess = settings.AllowAnonymousSchemaAccess;
        }).Location("Content")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, OpenApiSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, OpenApiPermissions.ManageOpenApi))
        {
            return null;
        }

        var model = new OpenApiSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.AllowAnonymousSchemaAccess = model.AllowAnonymousSchemaAccess;

        _shellReleaseManager.RequestRelease();

        return await EditAsync(site, settings, context);
    }
}
