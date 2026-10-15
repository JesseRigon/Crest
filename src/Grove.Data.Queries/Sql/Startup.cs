using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Data.Queries;
using Grove.Data.Queries.Sql.Migrations;
using Grove.Security.Permissions;

namespace Grove.Data.Queries.Sql;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[Feature("Grove.Data.Queries.Sql")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddQuerySource<SqlQuerySource>(SqlQuerySource.SourceName);

        services.AddDataMigration<SqlQueryMigrations>();
        services.AddScoped<IQueryHandler, SqlQueryHandler>();
        services.AddScoped<SqlQueryRunner>();
    }
}
