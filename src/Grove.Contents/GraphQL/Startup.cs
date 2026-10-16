using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management.GraphQL;
using Grove.Modules;

namespace Grove.Contents.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentGraphQL();

        // Each embedding a field on its parent type's object.
        services.AddScoped<Grove.Contents.Management.GraphQL.Queries.Types.IContentTypeBuilder, EmbeddedContentTypeBuilder>();
    }
}
