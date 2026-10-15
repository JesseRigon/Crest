using Grove.Display.Management.Document;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Integrations.Facebook.Endpoints;
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
            group => group.InMenu("settings-integrations", entryId: "facebookApp").ReloadsTenant());
        services.AddRecipeExecutionStep<FacebookSettingsStep>();

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<FacebookSettings>, FacebookSettingsConfiguration>();

        services.AddDocumentContributor<FacebookSdkDocumentContributor>();
    }
}
