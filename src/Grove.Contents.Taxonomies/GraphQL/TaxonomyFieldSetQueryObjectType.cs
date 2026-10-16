using GraphQL.Types;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management;
using Grove.Contents.Management.GraphQL.Queries.Types;
using Grove.Contents.Taxonomies.Models;

namespace Grove.Contents.Taxonomies.GraphQL;

public class TaxonomyFieldSetQueryObjectType : ObjectGraphType<TaxonomyFieldSet>
{
    public TaxonomyFieldSetQueryObjectType(IStringLocalizer<TaxonomyFieldSetQueryObjectType> S)
    {
        Name = "TaxonomyFieldSet";

        Field(x => x.TermContentType);

        Field<ListGraphType<ContentItemInterface>, IEnumerable<ContentItem>>("contentItems")
            .Description(S["the content items"])
            .PagingArguments()
            .Resolve(x => x.Page(TaxonomyFieldSet.Terms(x.Source.ContentItem)));
    }
}
