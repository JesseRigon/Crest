using GraphQL.Types;
using Grove.Contents.SiteMenu.Models;

namespace Grove.Contents.SiteMenu.GraphQL;

public class LinkMenuItemQueryObjectType : ObjectGraphType<LinkMenuItemPart>
{
    public LinkMenuItemQueryObjectType()
    {
        Name = "LinkMenuItemPart";

        Field(x => x.Url, nullable: true);
    }
}
