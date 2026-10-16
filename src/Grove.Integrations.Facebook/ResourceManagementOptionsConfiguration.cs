using Microsoft.Extensions.Options;
using Grove.Integrations.Facebook.Endpoints;
using Grove.Integrations.Facebook.Services;
using Grove.Integrations.Facebook.Settings;
using Grove.Display.Resources.Management;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Facebook;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private readonly ISiteService _siteService;
    private readonly IFacebookService _facebookService;

    public ResourceManagementOptionsConfiguration(ISiteService siteService, IFacebookService facebookService)
    {
        _siteService = siteService;
        _facebookService = facebookService;
    }

    public void Configure(ResourceManagementOptions options)
    {
        var settings = _siteService.GetSettings<FacebookSettings>();
        var appId = _facebookService.GetAppIdAsync().GetAwaiter().GetResult();

        var manifest = new ResourceManifest();

        manifest
            .DefineScript("fb")
            .SetDependencies("fbsdk")
            .SetUrl($"~/Grove.Integrations.Facebook/sdk/init.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings, appId)}");

        manifest
            .DefineScript("fbsdk")
            .SetCultures(GetSdkEndpoints.GetFetchScriptEndpoint.ValidFacebookCultures)
            .SetUrl($"~/Grove.Integrations.Facebook/sdk/sdk.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings, appId)}");

        options.ResourceManifests.Add(manifest);
    }
}
