using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Drivers;
using Crest.Security.Permissions;
using Crest.Security.Services;
using Crest.Security.Settings;
using Crest.Settings.Deployment;

namespace Crest.Security;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.Security;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<SecurityPermissions>();
        services.AddSiteDisplayDriver<SecuritySettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddSingleton<ISecurityService, SecurityService>();

        services.AddTransient<IConfigureOptions<SecuritySettings>, SecuritySettingsConfiguration>();
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var securityOptions = serviceProvider.GetRequiredService<IOptions<SecuritySettings>>().Value;

        builder.UseSecurityHeaders(options =>
        {
            options
                .AddContentSecurityPolicy(securityOptions.ContentSecurityPolicy)
                .AddContentTypeOptions()
                .AddPermissionsPolicy(securityOptions.PermissionsPolicy)
                .AddReferrerPolicy(securityOptions.ReferrerPolicy);
        });
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class SecurityDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<SecuritySettings, SecurityDeploymentStartup>(
            S => S["Security settings"],
            S => S["Exports the Security settings."]);
    }
}
