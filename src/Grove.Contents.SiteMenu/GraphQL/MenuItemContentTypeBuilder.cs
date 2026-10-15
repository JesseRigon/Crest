using GraphQL.Types;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.SiteMenu.Models;

namespace Grove.Contents.SiteMenu.GraphQL;

public class MenuItemContentTypeBuilder : IContentTypeBuilder
{
    public void Build(ISchema schema, FieldType contentQuery, ContentTypeDefinition contentTypeDefinition, ContentItemType contentItemType)
    {
        if (contentTypeDefinition.GetStereotype() != "MenuItem")
        {
            return;
        }

        contentItemType
            .Field<MenuItemsListQueryObjectType>(nameof(MenuItemsListPart).ToFieldName())
            .Resolve(context => context.Source.TryGet<MenuItemsListPart>(out var menuItemsListPart) ? menuItemsListPart : null);

        contentItemType.Interface<MenuItemInterface>();
    }

    public void Clear()
    {
    }
}
