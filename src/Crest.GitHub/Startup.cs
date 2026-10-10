using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.GitHub.Configuration;
using Crest.GitHub.Drivers;
using Crest.GitHub.Recipes;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.GitHub;

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
        services.AddSiteDisplayDriver<GitHubAuthenticationSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenuGitHubLogin>();

        // Register the options initializers required by the GitHub Handler.
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, AuthenticationOptionsConfiguration>();

        services.AddTransient<IConfigureOptions<GitHubAuthenticationOptions>, GitHubAuthenticationOptionsConfiguration>();

        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<GitHubAuthenticationOptions>, OAuthPostConfigureOptions<GitHubAuthenticationOptions, GitHubAuthenticationHandler>>();
    }
}

[Feature(GitHubConstants.Features.GitHubAuthentication)]
[RequireFeatures("Crest.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<GitHubAuthenticationSettingsStep>();
    }
}
