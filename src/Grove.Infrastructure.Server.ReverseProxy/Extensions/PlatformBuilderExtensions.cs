using Microsoft.Extensions.Configuration;
using Grove.Environment.Shell.Configuration;
using Grove.Infrastructure.Server.ReverseProxy.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureReverseProxySettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Grove_ReverseProxy");

            tenantServices.PostConfigure<ReverseProxySettings>(settings =>
            {
                configurationSection.Bind(settings);

                settings.FromConfiguration = true;
            });
        });

        return builder;
    }
}
