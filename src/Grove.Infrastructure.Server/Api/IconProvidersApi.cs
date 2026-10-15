using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Display.Icons.Iconify;
using Grove.Display.Icons;
using Microsoft.AspNetCore.Authorization;
using Grove.Infrastructure.Settings;

namespace Grove.Api;

/// <summary>The host's <c>api/grove/icons/providers</c> endpoints.</summary>
public sealed class IconProvidersApi : ApiContributor
{
    public override string Name => "IconProviders";

    public override string FeatureId => "Grove";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("icons/providers");
        routes.MapGet("", ApiHandlers.Handle<IconProvidersHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext http, [FromBody] IconProvidersSettings settings) => ApiHandlers.For<IconProvidersHandlers>(http).PutAsync(settings));
        routes.MapGet("iconify/local", ApiHandlers.Handle<IconProvidersHandlers>(h => h.GetIconifyLocalStatusAsync()));
    }
}

public sealed class IconProvidersHandlers(
    IAuthorizationService authorizationService,
    IIconProviderSettingsStore settingsStore,
    IIconifyLocalMirrorStore localMirrorStore) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return Ok(Redact(await settingsStore.GetAsync(HttpContext.RequestAborted)));
    }

    public async Task<IResult> PutAsync(IconProvidersSettings settings)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (!Uri.TryCreate(settings.Iconify.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return ApiProblems.Validation("iconify.baseUrl", "Iconify base URL must be an absolute http or https URL.");
        }

        var existing = await settingsStore.GetAsync(HttpContext.RequestAborted);
        var merged = settings with
        {
            Iconify = settings.Iconify with
            {
                ApiKey = string.IsNullOrWhiteSpace(settings.Iconify.ApiKey) ? existing.Iconify.ApiKey : settings.Iconify.ApiKey,
            },
        };

        return Ok(Redact(await settingsStore.SaveAsync(merged, HttpContext.RequestAborted)));
    }

    public async Task<IResult> GetIconifyLocalStatusAsync()
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return Ok(await localMirrorStore.GetStatusAsync(HttpContext.RequestAborted));
    }

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, SettingsPermissions.ManageSettings);

    private static IconProvidersSettings Redact(IconProvidersSettings settings) => settings with
    {
        Iconify = settings.Iconify with
        {
            ApiKey = null,
        },
    };
}
