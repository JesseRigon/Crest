using Microsoft.Extensions.DependencyInjection;
using Grove.Apis.GraphQL;
using Grove.Modules;
using Grove.Queries.Sql.GraphQL.Queries;

namespace Grove.Queries.Sql.GraphQL;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[Feature("Grove.Queries.Sql")]
[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SqlQueryFieldTypeProvider>();
    }
}
