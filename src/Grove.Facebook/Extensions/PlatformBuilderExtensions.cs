using Microsoft.Extensions.Configuration;
using Grove.Environment.Shell.Configuration;
using Grove.Facebook.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureFacebookSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Grove_Facebook");

            tenantServices.PostConfigure<FacebookSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
