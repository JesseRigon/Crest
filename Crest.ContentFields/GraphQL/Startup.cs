using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.ContentFields.Fields;
using Crest.ContentFields.GraphQL.Fields;
using Crest.ContentFields.GraphQL.Types;
using Crest.ContentManagement.GraphQL;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Modules;

namespace Crest.ContentFields.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IContentFieldProvider, ObjectGraphTypeFieldProvider>();
        services.AddTransient<IContentFieldProvider, ContentFieldsProvider>();

        services.AddObjectGraphType<LinkField, LinkFieldQueryObjectType>();
        services.AddObjectGraphType<HtmlField, HtmlFieldQueryObjectType>();
        services.AddObjectGraphType<ContentPickerField, ContentPickerFieldQueryObjectType>();
        services.AddObjectGraphType<UserPickerField, UserPickerFieldQueryObjectType>();
    }
}

[RequireFeatures("Crest.Apis.GraphQL", "Crest.ContentFields.Indexing.SQL")]
public class IndexStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentFieldsInputGraphQL();
    }
}
