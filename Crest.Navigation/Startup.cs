using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;

namespace Crest.Navigation;

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

        var navigationConfiguration = _shellConfiguration.GetSection("Crest_Navigation");
        services.Configure<PagerOptions>(navigationConfiguration.GetSection("PagerOptions"));
    }
}
