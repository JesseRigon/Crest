using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data.Connectors;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Integrations.GitHub.Configuration;
using Grove.Integrations.GitHub.Connections;
using Grove.Integrations.GitHub.Recipes;
using Grove.Integrations.GitHub.Services;
using Grove.Integrations.GitHub.Settings;
using Grove.Modules;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Integrations.GitHub;

/// <summary>The GitHub connection and the GitHub app credentials it shares with GitHub sign-in.</summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddGitHubConnection();
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
            group => group
                .InMenu("settings-authentication", entryId: "github")
                .Describe("The GitHub app's client id and secret are the GitHub app credentials on the Connections screen.")
                .ReloadsTenant());

        services.AddScoped<IConnectionChangeHandler>(sp => new AppCredentialsChangeHandler(
            sp.GetRequiredService<IShellReleaseManager>(), GitHubConstants.AppCredentialsKey, GitHubConstants.ConnectionKey));
        services.AddDataMigration<GitHubAuthenticationSettingsMigrations>();

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

/// <summary>Moves the GitHub client id and secret out of the sign-in settings into the GitHub app credentials.</summary>
internal sealed class GitHubAuthenticationSettingsMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => AppCredentialsAccess.MoveFromSiteSettingsAsync(
            scope.ServiceProvider,
            nameof(GitHubAuthenticationSettings),
            "ClientID",
            "ClientSecret",
            GitHubConstants.Features.GitHubAuthentication,
            GitHubConstants.AppCredentialsKey,
            scope.ServiceProvider.GetRequiredService<ILogger<GitHubAuthenticationSettingsMigrations>>()));

        return 1;
    }
}
