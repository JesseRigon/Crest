using GraphQL.Types;
using Grove.ContentManagement;

namespace Grove.Menu.GraphQL;

public class MenuItemInterface : InterfaceGraphType<ContentItem>
{
    public MenuItemInterface()
    {
        Name = "MenuItem";
        Field<MenuItemsListQueryObjectType>("menuItemsList");
    }
}
