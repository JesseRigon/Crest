using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Protocols.GraphQL;
using Grove.Modules;
using Grove.Data.Queries.Sql.GraphQL.Queries;

namespace Grove.Data.Queries.Sql.GraphQL;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[Feature("Grove.Data.Queries.Sql")]
[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SqlQueryFieldTypeProvider>();
    }
}
