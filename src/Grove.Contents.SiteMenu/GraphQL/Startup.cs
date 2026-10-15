using Microsoft.Extensions.DependencyInjection;
using Grove.Apis;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.SiteMenu.Models;
using Grove.Modules;

namespace Grove.Contents.SiteMenu.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL")]
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
