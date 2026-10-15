using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;

namespace Grove.BackgroundTasks;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<BackgroundTaskManager>()
            .AddPermissionProvider<Permissions>()
            .AddScoped<IBackgroundTaskSettingsProvider, BackgroundTaskSettingsProvider>();
    }
}
