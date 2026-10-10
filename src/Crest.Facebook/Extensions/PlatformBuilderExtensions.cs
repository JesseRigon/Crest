using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.Facebook.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureFacebookSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_Facebook");

            tenantServices.PostConfigure<FacebookSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
