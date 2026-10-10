using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Flows.Models;
using Crest.Modules;

namespace Crest.Flows.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<BagPart, BagPartQueryObjectType>();
        services.AddObjectGraphType<FlowPart, FlowPartQueryObjectType>();
        services.AddObjectGraphType<FlowMetadata, FlowMetadataQueryObjectType>();

        services.AddScoped<IContentTypeBuilder, FlowMetadataContentTypeBuilder>();
    }
}
