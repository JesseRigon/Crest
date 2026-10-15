using GraphQL.Types;
using Grove.Menu.Models;

namespace Grove.Menu.GraphQL;

public class HtmlMenuItemQueryObjectType : ObjectGraphType<HtmlMenuItemPart>
{
    public HtmlMenuItemQueryObjectType()
    {
        Name = "HtmlMenuItemPart";

        Field(x => x.Url, nullable: true);
        Field(x => x.Target, nullable: true);
        Field(x => x.Html, nullable: true);
    }
}
