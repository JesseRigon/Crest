using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Descriptors;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;

namespace Grove.Navigation;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigation();

        services.AddShapeTableProvider<NavigationShapes>();
        services.AddShapeTableProvider<PagerShapesTableProvider>();
        services.AddShapeAttributes<PagerShapes>();

        var navigationConfiguration = _shellConfiguration.GetSection("Grove_Navigation");
        services.Configure<PagerOptions>(navigationConfiguration.GetSection("PagerOptions"));
    }
}
