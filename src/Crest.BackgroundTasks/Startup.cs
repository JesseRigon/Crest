using Microsoft.Extensions.DependencyInjection;
using Crest.BackgroundTasks.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;

namespace Crest.BackgroundTasks;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<BackgroundTaskManager>()
            .AddPermissionProvider<Permissions>()
            .AddNavigationProvider<AdminMenu>()
            .AddScoped<IBackgroundTaskSettingsProvider, BackgroundTaskSettingsProvider>();
    }
}
