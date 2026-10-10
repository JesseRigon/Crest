using Microsoft.Extensions.Configuration;
using Crest.Environment.Shell.Configuration;
using Crest.GitHub.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureGitHubSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_GitHub");

            tenantServices.PostConfigure<GitHubAuthenticationSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
