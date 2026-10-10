using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Security.Permissions;

namespace Crest.Global;

[Feature("Crest.Global")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<CrestGlobalPermissions>();
    }
}
