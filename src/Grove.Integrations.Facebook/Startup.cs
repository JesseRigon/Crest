using Grove.Display.Management.Document;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Data.Migration;
using Grove.Environment.Shell.Scope;
using Grove.Integrations.Facebook.Connections;
using Grove.Integrations.Facebook.Endpoints;
using Microsoft.Extensions.Logging;
using Grove.Integrations.Facebook.Filters;
using Grove.Integrations.Facebook.Recipes;
using Grove.Integrations.Facebook.Services;
using Grove.Integrations.Facebook.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Integrations.Facebook;

public sealed class Startup : StartupBase
{
    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddSdkEndpoints();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddSingleton<IFacebookService, FacebookService>();
        services.AddSettingsGroup<FacebookSettings>(
            FacebookConstants.Features.Core,
            "Meta App",
            Permissions.ManageFacebookApp,
            group => group
                .InMenu("settings-integrations", entryId: "facebookApp")
                .Describe("The Meta app id and secret are the Meta app credentials on the Connections screen.")
                .ReloadsTenant());
        services.AddRecipeExecutionStep<FacebookSettingsStep>();

        services.AddGraphConnection();
        services.AddDataMigration<FacebookSettingsMigrations>();
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        services.AddDocumentContributor<FacebookSdkDocumentContributor>();
    }
}

/// <summary>Moves the Meta app id and secret out of the Meta settings into the Meta app credentials.</summary>
internal sealed class FacebookSettingsMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => AppCredentialsAccess.MoveFromSiteSettingsAsync(
            scope.ServiceProvider,
            nameof(FacebookSettings),
            "AppId",
            "AppSecret",
            FacebookConstants.Features.Core,
            FacebookConstants.AppCredentialsKey,
            scope.ServiceProvider.GetRequiredService<ILogger<FacebookSettingsMigrations>>()));

        return 1;
    }
}
