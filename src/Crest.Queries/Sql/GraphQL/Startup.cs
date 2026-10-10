using Microsoft.Extensions.DependencyInjection;
using Crest.Apis.GraphQL;
using Crest.Modules;
using Crest.Queries.Sql.GraphQL.Queries;

namespace Crest.Queries.Sql.GraphQL;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[Feature("Crest.Queries.Sql")]
[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, SqlQueryFieldTypeProvider>();
    }
}
