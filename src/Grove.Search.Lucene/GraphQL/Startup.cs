using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Protocols.GraphQL;
using Grove.Modules;
using Grove.Data.Queries.Lucene.GraphQL.Queries;

namespace Grove.Search.Lucene.GraphQL;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[RequireFeatures("Grove.Data.Protocols.GraphQL", "Grove.Data.Queries")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, LuceneQueryFieldTypeProvider>();
    }
}
