using GraphQL.Types;
using Grove.ContentManagement;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.ContentManagement.Metadata.Models;
using Grove.Menu.Models;

namespace Grove.Menu.GraphQL;

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
