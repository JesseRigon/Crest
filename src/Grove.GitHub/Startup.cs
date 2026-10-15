using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.GitHub.Configuration;
using Grove.GitHub.Drivers;
using Grove.GitHub.Recipes;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.GitHub;

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
        services.AddPageSource<GitHubLoginPages>();

        // Register the options initializers required by the GitHub Handler.
        services.AddTransient<IConfigureOptions<AuthenticationOptions>, AuthenticationOptionsConfiguration>();

        services.AddTransient<IConfigureOptions<GitHubAuthenticationOptions>, GitHubAuthenticationOptionsConfiguration>();

        // Built-in initializers:
        services.AddTransient<IPostConfigureOptions<GitHubAuthenticationOptions>, OAuthPostConfigureOptions<GitHubAuthenticationOptions, GitHubAuthenticationHandler>>();
    }
}

[Feature(GitHubConstants.Features.GitHubAuthentication)]
[RequireFeatures("Grove.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<GitHubAuthenticationSettingsStep>();
    }
}
