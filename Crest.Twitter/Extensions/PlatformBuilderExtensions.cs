using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.Twitter.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureTwitterSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configuration = serviceProvider.GetRequiredService<IShellConfiguration>();
            var configurationSection = configuration.GetSection("Crest_X");

            if (configurationSection.Value is null)
            {
                configurationSection = configuration.GetSection("Crest_Twitter");
            }

            tenantServices.PostConfigure<TwitterSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
