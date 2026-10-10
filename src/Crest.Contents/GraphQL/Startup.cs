using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement.GraphQL;
using Crest.Modules;

namespace Crest.Contents.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentGraphQL();
    }
}
