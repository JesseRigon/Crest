using Microsoft.Extensions.Options;
using Grove.Facebook.Endpoints;
using Grove.Facebook.Settings;
using Grove.ResourceManagement;
using Grove.Settings;

namespace Grove.Facebook;

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
            .SetUrl($"~/Grove.Facebook/sdk/init.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings)}");

        manifest
            .DefineScript("fbsdk")
            .SetCultures(GetSdkEndpoints.GetFetchScriptEndpoint.ValidFacebookCultures)
            .SetUrl($"~/Grove.Facebook/sdk/sdk.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings)}");

        options.ResourceManifests.Add(manifest);
    }
}
