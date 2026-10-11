using Crest.Access;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Modules;
using Crest.Security.Permissions;
using Crest.Sharing.Indexes;
using Crest.Sharing.Migrations;
using Crest.Sharing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Sharing;

/// <summary>
/// Sharing registers with the access gate once, for every shareable kind a module registers
/// (docs/sharing.md › Ruling): the authoritative mapper. Turning the feature off removes it,
/// and each kind's module decides with its own permissions again.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ShareableKinds>();
        services.AddScoped<IShareStore, YesSqlShareStore>();
        services.AddScoped<ISharingResolver, SharingResolver>();
        services.AddScoped<ISharingService, SharingService>();
        services.AddScoped<IResourcePermissionMapper, SharingPermissionMapper>();
        services.AddPermissionProvider<Permissions>();
        services.AddIndexProvider<ShareIndexProvider>();
        services.AddDataMigration<SharingMigrations>();
    }
}
