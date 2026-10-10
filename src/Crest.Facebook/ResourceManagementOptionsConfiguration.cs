using Microsoft.Extensions.Options;
using Crest.Facebook.Endpoints;
using Crest.Facebook.Settings;
using Crest.ResourceManagement;
using Crest.Settings;

namespace Crest.Facebook;

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
            .SetUrl($"~/Crest.Facebook/sdk/init.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings)}");

        manifest
            .DefineScript("fbsdk")
            .SetCultures(GetSdkEndpoints.GetFetchScriptEndpoint.ValidFacebookCultures)
            .SetUrl($"~/Crest.Facebook/sdk/sdk.js?v={GetSdkEndpoints.GetInitScriptEndpoint.HashCacheBustingValues(settings)}");

        options.ResourceManifests.Add(manifest);
    }
}
