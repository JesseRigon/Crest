using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.BagParts.Models;

namespace Grove.Contents.BagParts.GraphQL;

public class BagPartQueryObjectType : ObjectGraphType<BagPart>
{
    public BagPartQueryObjectType(IStringLocalizer<BagPartQueryObjectType> S)
    {
        Name = "BagPart";
        Description = S["A BagPart allows to add content items directly within another content item"];

        Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>("contentItems")
            .Description(S["the content items"])
            .PagingArguments()
            .Resolve(x => x.Page(x.Source.ContentItems));
    }
}
