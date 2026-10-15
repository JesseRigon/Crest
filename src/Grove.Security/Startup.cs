using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Security.Services;
using Grove.Security.Settings;
using Grove.Infrastructure.Settings.Deployment;

namespace Grove.Security;

public sealed class Startup : StartupBase
{
    public override int Order
        => PlatformConstants.ConfigureOrder.Security;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<SecurityPermissions>();
        services.AddApiContributor<Api.SecurityHeadersApi>();
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

[RequireFeatures("Grove.Deployment")]
public sealed class SecurityDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<SecuritySettings>();
    }
}
