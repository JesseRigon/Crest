using Microsoft.Extensions.DependencyInjection;
using Crest.Admin.Models;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Themes.TheAdmin.Drivers;

namespace Crest.Themes.TheAdmin;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Navbar, ToggleThemeNavbarDisplayDriver>();
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
