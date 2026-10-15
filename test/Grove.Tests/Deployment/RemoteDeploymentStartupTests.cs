using Grove.Security.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Tests.Deployment;

/// <summary>
/// Remote Deployment registers its own permissions (ManageRemoteInstances, ManageRemoteClients,
/// ...): its Startup once sat in the Deployment module's namespace, where a bare
/// <c>Permissions</c> named the Deployment module's provider, so the remote pages declared
/// permissions no feature registered.
/// </summary>
public class RemoteDeploymentStartupTests
{
    [Fact]
    public async Task RegistersTheRemoteDeploymentPermissions()
    {
        var services = new ServiceCollection();
        new global::Grove.Deployment.Remote.Startup().ConfigureServices(services);

        var providers = services
            .Where(descriptor => descriptor.ServiceType == typeof(IPermissionProvider))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        Assert.Contains(typeof(global::Grove.Deployment.Remote.Permissions), providers);

        var names = (await new global::Grove.Deployment.Remote.Permissions().GetPermissionsAsync()).Select(permission => permission.Name);
        Assert.Contains("ManageRemoteInstances", names);
        Assert.Contains("ManageRemoteClients", names);
    }
}
