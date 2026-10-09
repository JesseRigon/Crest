using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.Security.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureSecuritySettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_Security");

            tenantServices.PostConfigure<SecuritySettings>(settings =>
            {
                settings.ContentSecurityPolicy.Clear();
                settings.PermissionsPolicy.Clear();

                configurationSection.Bind(settings);

                settings.FromConfiguration = true;
            });
        });

        return builder;
    }
}
