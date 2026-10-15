using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Integrations.GitHub.Configuration;
using Grove.Integrations.GitHub.Recipes;
using Grove.Integrations.GitHub.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Integrations.GitHub;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
    }
}

[Feature(GitHubConstants.Features.GitHubAuthentication)]
public sealed class GitHubLoginStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsGroup<GitHubAuthenticationSettings>(
            GitHubConstants.Features.GitHubAuthentication,
            "GitHub",
            Permissions.ManageGitHubAuthentication,
            group => group.InMenu("settings-authentication", entryId: "github").ReloadsTenant());

        // Register the options initializers required by the GitHub Handler.
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, AuthenticationOptionsConfiguration>();

        services.AddTransient<IConfigureOptions<GitHubAuthenticationOptions>, GitHubAuthenticationOptionsConfiguration>();

        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<GitHubAuthenticationOptions>, OAuthPostConfigureOptions<GitHubAuthenticationOptions, GitHubAuthenticationHandler>>();
    }
}

[Feature(GitHubConstants.Features.GitHubAuthentication)]
[RequireFeatures("Grove.Infrastructure.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<GitHubAuthenticationSettingsStep>();
    }
}
