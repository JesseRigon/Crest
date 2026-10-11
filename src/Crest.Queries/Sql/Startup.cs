using Microsoft.Extensions.DependencyInjection;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Queries;
using Crest.Queries.Sql.Drivers;
using Crest.Queries.Sql.Migrations;
using Crest.Security.Permissions;

namespace Crest.Queries.Sql;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[Feature("Crest.Queries.Sql")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddDisplayDriver<Query, SqlQueryDisplayDriver>();
        services.AddQuerySource<SqlQuerySource>(SqlQuerySource.SourceName);

        services.AddNavigationProvider<AdminMenu>();
        services.AddDataMigration<SqlQueryMigrations>();
        services.AddScoped<IQueryHandler, SqlQueryHandler>();
    }
}
