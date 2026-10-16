using GraphQL.Types;
using Grove.Contents.SiteMenu.Models;

namespace Grove.Contents.SiteMenu.GraphQL;

public class LinkMenuItemQueryObjectType : ObjectGraphType<LinkMenuItemFieldSet>
{
    public LinkMenuItemQueryObjectType()
    {
        Name = "LinkMenuItemFieldSet";

        Field(x => x.Url, nullable: true);
    }
}
