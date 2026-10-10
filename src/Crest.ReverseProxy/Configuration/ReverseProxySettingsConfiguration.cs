using Microsoft.Extensions.Options;
using Crest.ReverseProxy.Settings;
using Crest.Settings;

namespace Crest.ReverseProxy.Configuration;

internal sealed class ReverseProxySettingsConfiguration : IConfigureOptions<ReverseProxySettings>
{
    private readonly ISiteService _siteService;

    public ReverseProxySettingsConfiguration(ISiteService siteService)
        => _siteService = siteService;

    public void Configure(ReverseProxySettings options)
    {
        var settings = _siteService.GetSettings<ReverseProxySettings>();

        options.ForwardedHeaders = settings.ForwardedHeaders;
        options.KnownNetworks = settings.KnownNetworks;
        options.KnownProxies = settings.KnownProxies;
    }
}
