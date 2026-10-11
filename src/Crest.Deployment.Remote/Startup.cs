using Crest.Access;
using Crest.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Deployment.Remote;
using Crest.Deployment.Remote.Services;
using Crest.FileStorage;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;

namespace Crest.Deployment;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.TryAddTransient<FileCreationService>();

        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<RemoteInstanceService>();
        services.AddScoped<RemoteClientService>();
        services.AddScoped<IDeploymentTargetProvider, RemoteInstanceDeploymentTargetProvider>();
        services.AddPermissionProvider<Permissions>();

        // The remote deployment key as an Api credential: the gate authenticates it through
        // the forwarder; the import runs as the remote client's caller.
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, RemoteDeploymentKeyAuthenticationHandler>(RemoteDeploymentKeyAuthenticationHandler.Scheme, null);
        services.Configure<ApiAuthorizationOptions>(PlatformConstants.AuthenticationSchemes.Api, options => options.AdditionalSchemes.Add(RemoteDeploymentKeyAuthenticationHandler.Scheme));
        services.AddScoped<ICallerContextContributor, RemoteDeploymentCallerContributor>();
    }
}
