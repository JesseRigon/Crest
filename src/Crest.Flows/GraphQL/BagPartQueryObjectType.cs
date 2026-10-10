using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL;
using Crest.ContentManagement;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Flows.Models;

namespace Crest.Flows.GraphQL;

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
