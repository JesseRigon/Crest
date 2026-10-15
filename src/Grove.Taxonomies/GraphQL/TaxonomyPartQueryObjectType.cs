using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Apis.GraphQL;
using Grove.ContentManagement;
using Grove.ContentManagement.GraphQL.Queries.Types;
using Grove.Taxonomies.Models;

namespace Grove.Taxonomies.GraphQL;

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
