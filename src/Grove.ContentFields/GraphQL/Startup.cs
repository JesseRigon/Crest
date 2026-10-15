using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.ContentFields.Fields;
using Grove.ContentFields.GraphQL.Fields;
using Grove.ContentFields.GraphQL.Types;
using Grove.ContentManagement.GraphQL;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.Modules;

namespace Grove.ContentFields.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IContentFieldProvider, ObjectGraphTypeFieldProvider>();
        services.AddTransient<IContentFieldProvider, ContentFieldsProvider>();

        services.AddObjectGraphType<LinkField, LinkFieldQueryObjectType>();
        services.AddObjectGraphType<HtmlField, HtmlFieldQueryObjectType>();
        services.AddObjectGraphType<UserPickerField, UserPickerFieldQueryObjectType>();
    }
}

[RequireFeatures("Grove.Apis.GraphQL", "Grove.ContentFields.Indexing.SQL")]
public class IndexStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentFieldsInputGraphQL();
    }
}
