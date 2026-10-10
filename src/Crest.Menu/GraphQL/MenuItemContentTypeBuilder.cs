using GraphQL.Types;
using Crest.ContentManagement;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.ContentManagement.Metadata.Models;
using Crest.Menu.Models;

namespace Crest.Menu.GraphQL;

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
