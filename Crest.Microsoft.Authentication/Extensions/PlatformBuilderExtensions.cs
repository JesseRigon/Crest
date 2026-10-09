using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.Microsoft.Authentication.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureMicrosoftAccountSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_Microsoft_Authentication_MicrosoftAccount");

            tenantServices.PostConfigure<MicrosoftAccountSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }

    public static PlatformBuilder ConfigureAzureADSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_Microsoft_Authentication_AzureAD");

            tenantServices.PostConfigure<AzureADSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
