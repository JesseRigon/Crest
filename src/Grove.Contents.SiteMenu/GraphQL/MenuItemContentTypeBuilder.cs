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

        // A menu item's child items: the menu's nested MenuItems embedding, under the item.
        contentItemType
            .Field<ListGraphType<MenuItemInterface>>("menuItems")
            .Resolve(context => MenuItemsEmbedding.Items(context.Source));

        contentItemType.Interface<MenuItemInterface>();
    }

    public void Clear()
    {
    }
}
