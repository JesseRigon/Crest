using Microsoft.Extensions.Options;
using Grove.Integrations.Facebook.Endpoints;
using Grove.Integrations.Facebook.Settings;
using Grove.Display.Resources.Management;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Facebook;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private readonly ISiteService _siteService;

    public ResourceManagementOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void Configure(ResourceManagementOptions options)
    {
        var settings = _siteService.GetSettings<FacebookSettings>();

        var manifest = new ResourceManifest();

        manifest
            .DefineScript("fb")
            .SetDependencies("fbsdk")
            .SetUrl($"~/Grove.Integrations.Facebook/sdk/init.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings)}");

        manifest
            .DefineScript("fbsdk")
            .SetCultures(GetSdkEndpoints.GetFetchScriptEndpoint.ValidFacebookCultures)
            .SetUrl($"~/Grove.Integrations.Facebook/sdk/sdk.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings)}");

        options.ResourceManifests.Add(manifest);
    }
}
