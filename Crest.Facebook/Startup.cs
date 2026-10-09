using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.Facebook.Drivers;
using Crest.Facebook.Endpoints;
using Crest.Facebook.Filters;
using Crest.Facebook.Recipes;
using Crest.Facebook.Services;
using Crest.Facebook.Settings;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.Facebook;

public sealed class Startup : StartupBase
{
    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddSdkEndpoints();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();

        services.AddSingleton<IFacebookService, FacebookService>();
        services.AddSiteDisplayDriver<FacebookSettingsDisplayDriver>();
        services.AddRecipeExecutionStep<FacebookSettingsStep>();

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddTransient<IConfigureOptions<FacebookSettings>, FacebookSettingsConfiguration>();

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<FBInitFilter>();
        });
    }
}
