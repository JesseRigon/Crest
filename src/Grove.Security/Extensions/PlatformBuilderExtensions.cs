using Microsoft.Extensions.Configuration;
using Grove.Environment.Shell.Configuration;
using Grove.Security.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureSecuritySettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Grove_Security");

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
