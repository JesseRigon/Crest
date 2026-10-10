using GraphQL.Types;
using Crest.Menu.Models;

namespace Crest.Menu.GraphQL;

public class LinkMenuItemQueryObjectType : ObjectGraphType<LinkMenuItemPart>
{
    public LinkMenuItemQueryObjectType()
    {
        Name = "LinkMenuItemPart";

        Field(x => x.Url, nullable: true);
    }
}
