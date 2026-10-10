using Microsoft.Extensions.DependencyInjection;
using Crest.Apis.GraphQL;
using Crest.Modules;
using Crest.Elasticsearch.GraphQL.Queries;

namespace Crest.Elasticsearch.GraphQL;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
[RequireFeatures("Crest.Apis.GraphQL", "Crest.Queries")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, ElasticsearchQueryFieldTypeProvider>();
    }
}
