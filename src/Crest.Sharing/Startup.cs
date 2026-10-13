using Crest.Access;
using Crest.BackgroundTasks;
using Crest.Data;
using Crest.Recipes;
using Crest.Environment.Shell;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Sharing.Indexes;
using Crest.Sharing.Migrations;
using Crest.Sharing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Sharing;

/// <summary>
/// Sharing registers with the access gate once, for every shareable kind a module registers
/// (docs/sharing.md › Ruling): the authoritative mapper, and the list scopes over the access
/// index for kinds whose objects are index rows. Turning the feature off removes it,
/// and each kind's module decides with its own permissions again.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ShareableKinds>();
        services.AddScoped<IShareStore, YesSqlShareStore>();
        services.AddScoped<SharingResolver>();
        services.AddScoped<ISharingResolver>(provider => provider.GetRequiredService<SharingResolver>());
        services.AddScoped<ISharingService, SharingService>();
        services.AddScoped<IShareChangeChecks, ShareChangeChecks>();
        services.AddScoped<IShareComments, ShareComments>();
        services.AddScoped<IShareIndicators, ShareIndicators>();
        services.AddScoped<ISharePrincipalDirectory, SharingPrincipalDirectory>();
        services.AddScoped<IResourcePermissionMapper, SharingPermissionMapper>();
        services.AddPermissionProvider<Permissions>();
        services.AddIndexProvider<ShareIndexProvider>();

        // The access index (phase 2): lists and search filter on it.
        services.AddScoped<IShareAccessStore, YesSqlShareAccessStore>();
        services.AddScoped<ShareAccessIndexer>();
        services.AddScoped<IShareAccessIndex>(provider => provider.GetRequiredService<ShareAccessIndexer>());
        services.AddScoped<IScopeProviderSource, SharingScopeProviderSource>();
        services.AddScoped<IShareScopes, SharingScopes>();
        services.AddScoped<IFeatureEventHandler, SharingFeatureEvents>();
        services.AddIndexProvider<ShareAccessIndexProvider>();
        services.AddIndexProvider<ShareRecalculationIndexProvider>();
        services.AddSingleton<IBackgroundTask, ShareAccessBackgroundTask>();
        services.AddRecipeExecutionStep<Recipes.SharingStep>();
        services.AddDataMigration<SharingMigrations>();

        // The interface (phase 4): the menu entry and the pages' gates.
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IRoutePermissionProvider, SharingRoutePermissions>();
    }
}
