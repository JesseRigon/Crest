using Grove.Access;
using Grove.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Deployment.Remote.Services;
using Grove.FileStorage;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;

namespace Grove.Deployment.Remote;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.TryAddTransient<FileCreationService>();

        services.AddScoped<RemoteInstanceService>();
        services.AddScoped<RemoteClientService>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IRemoteDeploymentTargets, Remote.Api.RemoteInstanceTargets>();
        services.AddApiContributor<Remote.Api.RemoteDeploymentApi>();

        // The remote deployment key as an Api credential: the gate authenticates it through
        // the forwarder; the import runs as the remote client's caller.
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, RemoteDeploymentKeyAuthenticationHandler>(RemoteDeploymentKeyAuthenticationHandler.Scheme, null);
        services.Configure<ApiAuthorizationOptions>(PlatformConstants.AuthenticationSchemes.Api, options => options.AdditionalSchemes.Add(RemoteDeploymentKeyAuthenticationHandler.Scheme));
        services.AddScoped<ICallerContextContributor, RemoteDeploymentCallerContributor>();
    }
}
