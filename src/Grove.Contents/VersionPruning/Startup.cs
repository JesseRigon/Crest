using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
using Grove.Contents.VersionPruning.Drivers;
using Grove.Contents.VersionPruning.Services;
using Grove.Contents.VersionPruning.Tasks;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;

namespace Grove.Contents.VersionPruning;

[Feature("Grove.Contents.VersionPruning")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentVersionPruningService, ContentVersionPruningService>();
        services.AddSingleton<IBackgroundTask, ContentVersionPruningBackgroundTask>();
        services.AddSiteDisplayDriver<ContentVersionPruningSettingsDisplayDriver>();
        services.AddPageSource<AdminPages>();
        services.AddPermissionProvider<ContentVersionPruningPermissionProvider>();
    }
}
