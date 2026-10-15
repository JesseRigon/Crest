using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Display.Resources.Management;

namespace Grove.Display.Resources;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        var resourceConfiguration = _shellConfiguration.GetSection("Grove_Resources");
        serviceCollection.Configure<ResourceOptions>(resourceConfiguration);

    }
}
