using Microsoft.Extensions.DependencyInjection;
using Grove.BackgroundTasks;
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
        services.AddSettingsGroup<Models.ContentVersionPruningSettings>(
            Models.ContentVersionPruningSettings.GroupId,
            "Content Version Pruning",
            ContentVersionPruningPermissions.ManageContentVersionPruningSettings,
            group => group.InMenu("settings", entryId: "content-version-pruning"));
        services.AddPermissionProvider<ContentVersionPruningPermissionProvider>();
    }
}
