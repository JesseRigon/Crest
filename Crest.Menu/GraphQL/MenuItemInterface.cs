using GraphQL.Types;
using Crest.ContentManagement;

namespace Crest.Menu.GraphQL;

public class MenuItemInterface : InterfaceGraphType<ContentItem>
{
    public MenuItemInterface()
    {
        Name = "MenuItem";
        Field<MenuItemsListQueryObjectType>("menuItemsList");
    }
}
