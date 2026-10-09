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
    }
}
