using Microsoft.Extensions.Configuration;
using Grove.Environment.Shell.Configuration;
using Grove.Integrations.Twitter.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureTwitterSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configuration = serviceProvider.GetRequiredService<IShellConfiguration>();
            var configurationSection = configuration.GetSection("Grove_X");

            if (configurationSection.Value is null)
            {
                configurationSection = configuration.GetSection("Grove_Twitter");
            }

            tenantServices.PostConfigure<TwitterSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
