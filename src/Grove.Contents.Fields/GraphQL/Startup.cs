using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.GraphQL.Fields;
using Grove.Contents.Fields.GraphQL.Types;
using Grove.Contents.Management.GraphQL;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Modules;

namespace Grove.Contents.Fields.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IContentFieldProvider, ObjectGraphTypeFieldProvider>();
        services.AddTransient<IContentFieldProvider, ContentFieldsProvider>();

        services.AddObjectGraphType<LinkField, LinkFieldQueryObjectType>();
        services.AddObjectGraphType<HtmlField, HtmlFieldQueryObjectType>();
    }
}

[RequireFeatures("Grove.Data.Protocols.GraphQL", "Grove.Contents.Fields.Indexing.SQL")]
public class IndexStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentFieldsInputGraphQL();
    }
}
