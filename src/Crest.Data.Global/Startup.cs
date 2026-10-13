using Microsoft.Extensions.DependencyInjection;
using Crest.Data.Global.Services;
using Crest.Modules;
using Crest.Security.Permissions;

namespace Crest.Data.Global;

[Feature("Crest.Data.Global")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<GlobalPermissions>();

        // The store is one database shared by every tenant: each tenant container hands out
        // the process-wide instance (GlobalHost), so enabling the feature is all a host
        // does. Factory registrations of non-disposable types, so a tenant teardown never
        // touches them.
        services.AddSingleton<IGlobalStore>(GlobalHost.Store);
        services.AddSingleton<IGlobalCache>(GlobalHost.Cache);
    }
}
