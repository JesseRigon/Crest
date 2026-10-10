using Microsoft.Extensions.DependencyInjection;
using Crest.ContentTypes.Editors;
using Crest.ContentTypes.GraphQL.Drivers;
using Crest.Modules;

namespace Crest.ContentTypes.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentTypeDefinitionDisplayDriver, GraphQLContentTypeSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, GraphQLContentTypePartSettingsDriver>();
    }
}
