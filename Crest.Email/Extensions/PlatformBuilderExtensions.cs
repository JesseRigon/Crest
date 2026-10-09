using Microsoft.Extensions.Configuration;
using Crest.Email;
using Crest.Environment.Shell.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    [Obsolete("This extension is now obsolete and will be removed in the next release. You can safely stop using it, but please keep providing valid settings in the configuration provider for continued functionality.")]
    public static PlatformBuilder ConfigureEmailSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_Email");

            tenantServices.Configure<DefaultSmtpOptions>(options =>
            {
                configurationSection.Bind(options);

                options.IsEnabled = options.ConfigurationExists();
            });
        });

        return builder;
    }
}
