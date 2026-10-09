using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL;
using Crest.ContentManagement;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Taxonomies.Models;

namespace Crest.Taxonomies.GraphQL;

public class TaxonomyPartQueryObjectType : ObjectGraphType<TaxonomyPart>
{
    public TaxonomyPartQueryObjectType(IStringLocalizer<TaxonomyPartQueryObjectType> S)
    {
        Name = "TaxonomyPart";

        Field(x => x.TermContentType);

        Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>("contentItems")
            .Description(S["the content items"])
            .PagingArguments()
            .Resolve(x => x.Page(x.Source.Terms));
    }
}
