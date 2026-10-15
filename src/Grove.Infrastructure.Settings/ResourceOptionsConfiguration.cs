using Microsoft.Extensions.Options;
using Grove.Display.Resources.Management;

namespace Grove.Infrastructure.Settings;

public class ResourceOptionsConfiguration : IPostConfigureOptions<ResourceOptions>
{
    private readonly ISiteService _siteService;

    public ResourceOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public void PostConfigure(string name, ResourceOptions options)
    {
        var site = _siteService.GetSiteSettings();

        options.ResourceDebugMode = site.ResourceDebugMode;
        options.UseCdn = site.UseCdn;
        options.CdnBaseUrl = site.CdnBaseUrl;
        options.AppendVersion = site.AppendVersion;
    }
}
