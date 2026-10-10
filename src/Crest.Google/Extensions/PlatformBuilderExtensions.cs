using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.Google.Authentication.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureGoogleSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_Google");

            tenantServices.PostConfigure<GoogleAuthenticationSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
