using Microsoft.Extensions.DependencyInjection;
using Grove.Admin.Models;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Themes.TheAdmin.Drivers;

namespace Grove.Themes.TheAdmin;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Navbar, ToggleThemeNavbarDisplayDriver>();
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
