using GraphQL.Types;
using Grove.Menu.Models;

namespace Grove.Menu.GraphQL;

public class LinkMenuItemQueryObjectType : ObjectGraphType<LinkMenuItemPart>
{
    public LinkMenuItemQueryObjectType()
    {
        Name = "LinkMenuItemPart";

        Field(x => x.Url, nullable: true);
    }
}
