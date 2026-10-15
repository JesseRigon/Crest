using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Queries;
using Grove.Queries.Sql.Drivers;
using Grove.Queries.Sql.Migrations;
using Grove.Security.Permissions;

namespace Grove.Queries.Sql;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[Feature("Grove.Queries.Sql")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddDisplayDriver<Query, SqlQueryDisplayDriver>();
        services.AddQuerySource<SqlQuerySource>(SqlQuerySource.SourceName);

        services.AddDataMigration<SqlQueryMigrations>();
        services.AddScoped<IQueryHandler, SqlQueryHandler>();
        services.AddScoped<SqlQueryRunner>();
    }
}
