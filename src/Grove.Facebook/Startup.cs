using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.Facebook.Drivers;
using Grove.Facebook.Endpoints;
using Grove.Facebook.Filters;
using Grove.Facebook.Recipes;
using Grove.Facebook.Services;
using Grove.Facebook.Settings;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.Facebook;

public sealed class Startup : StartupBase
{
    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddSdkEndpoints();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddPageSource<FacebookAppPages>();

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
