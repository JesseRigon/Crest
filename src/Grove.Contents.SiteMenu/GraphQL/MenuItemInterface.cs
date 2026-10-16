using GraphQL.Types;
using Grove.Contents.Management;

namespace Grove.Contents.SiteMenu.GraphQL;

public class MenuItemInterface : InterfaceGraphType<ContentItem>
{
    public MenuItemInterface()
    {
        Name = "MenuItem";
        Field<ListGraphType<MenuItemInterface>>("menuItems");
    }
}
