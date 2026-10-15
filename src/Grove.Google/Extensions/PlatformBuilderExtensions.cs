using Microsoft.Extensions.Configuration;
using Grove.Environment.Shell.Configuration;
using Grove.Google.Authentication.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureGoogleSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Grove_Google");

            tenantServices.PostConfigure<GoogleAuthenticationSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
