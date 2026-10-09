using Microsoft.Extensions.DependencyInjection;
using Crest.Apis;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Menu.Models;
using Crest.Modules;

namespace Crest.Menu.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddObjectGraphType<MenuItemsListPart, MenuItemsListQueryObjectType>();
        services.AddObjectGraphType<LinkMenuItemPart, LinkMenuItemQueryObjectType>();
        services.AddObjectGraphType<HtmlMenuItemPart, HtmlMenuItemQueryObjectType>();
        services.AddScoped<IContentTypeBuilder, MenuItemContentTypeBuilder>();
    }
}
