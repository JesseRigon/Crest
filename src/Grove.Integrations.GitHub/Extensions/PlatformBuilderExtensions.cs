using Microsoft.Extensions.Configuration;
using Grove.Environment.Shell.Configuration;
using Grove.Integrations.GitHub.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureGitHubSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Grove_GitHub");

            tenantServices.PostConfigure<GitHubAuthenticationSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
