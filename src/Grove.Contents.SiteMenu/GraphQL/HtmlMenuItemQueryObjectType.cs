using GraphQL.Types;
using Grove.Contents.SiteMenu.Models;

namespace Grove.Contents.SiteMenu.GraphQL;

public class HtmlMenuItemQueryObjectType : ObjectGraphType<HtmlMenuItemFieldSet>
{
    public HtmlMenuItemQueryObjectType()
    {
        Name = "HtmlMenuItemFieldSet";

        Field(x => x.Url, nullable: true);
        Field(x => x.Target, nullable: true);
        Field(x => x.Html, nullable: true);
    }
}
