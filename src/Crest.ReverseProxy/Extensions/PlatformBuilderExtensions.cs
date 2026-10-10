using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.ReverseProxy.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureReverseProxySettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_ReverseProxy");

            tenantServices.PostConfigure<ReverseProxySettings>(settings =>
            {
                configurationSection.Bind(settings);

                settings.FromConfiguration = true;
            });
        });

        return builder;
    }
}
