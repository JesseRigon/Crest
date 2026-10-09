using Microsoft.Extensions.DependencyInjection;
using Crest.BackgroundTasks;
using Crest.Contents.VersionPruning.Drivers;
using Crest.Contents.VersionPruning.Services;
using Crest.Contents.VersionPruning.Tasks;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;

namespace Crest.Contents.VersionPruning;

[Feature("Crest.Contents.VersionPruning")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentVersionPruningService, ContentVersionPruningService>();
        services.AddSingleton<IBackgroundTask, ContentVersionPruningBackgroundTask>();
        services.AddSiteDisplayDriver<ContentVersionPruningSettingsDisplayDriver>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddPermissionProvider<ContentVersionPruningPermissionProvider>();
    }
}
