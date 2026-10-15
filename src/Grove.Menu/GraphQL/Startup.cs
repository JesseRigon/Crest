using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.Menu.Models;
using Grove.Modules;

namespace Grove.Menu.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL")]
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
