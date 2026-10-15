using Microsoft.Extensions.DependencyInjection;
using Grove.Apis.GraphQL;
using Grove.Modules;
using Grove.Queries.Lucene.GraphQL.Queries;

namespace Grove.Lucene.GraphQL;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[RequireFeatures("Grove.Apis.GraphQL", "Grove.Queries")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, LuceneQueryFieldTypeProvider>();
    }
}
