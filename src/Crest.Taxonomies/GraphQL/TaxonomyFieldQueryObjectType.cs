using System.Text.Json.Nodes;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL;
using Crest.ContentManagement;
using Crest.ContentManagement.GraphQL.Queries.Types;
using Crest.Taxonomies.Fields;

namespace Crest.Taxonomies.GraphQL;

public class TaxonomyFieldQueryObjectType : ObjectGraphType<TaxonomyField>
{
    public TaxonomyFieldQueryObjectType(IStringLocalizer<TaxonomyFieldQueryObjectType> S)
    {
        Name = nameof(TaxonomyField);

        Field<ListGraphType<StringGraphType>, IEnumerable<string>>("termContentItemIds")
            .Description(S["term content item ids"])
            .PagingArguments()
            .Resolve(x =>
            {
                return x.Page(x.Source.TermContentItemIds);
            });

        Field<StringGraphType, string>("taxonomyContentItemId")
            .Description(S["taxonomy content item id"])
            .Resolve(x =>
            {
                return x.Source.TaxonomyContentItemId;
            });

        Field<ListGraphType<ContentItemInterface>, List<ContentItem>>("termContentItems")
            .Description(S["the term content items"])
            .PagingArguments()
            .ResolveLockedAsync(async x =>
            {
                var ids = x.Page(x.Source.TermContentItemIds);
                var contentManager = x.RequestServices.GetService<IContentManager>();

                var taxonomy = await contentManager.GetAsync(x.Source.TaxonomyContentItemId);

                if (taxonomy == null)
                {
                    return null;
                }

                var terms = new List<ContentItem>();

                foreach (var termContentItemId in ids)
                {
                    var term = TaxonomyPlatformHelperExtensions.FindTerm(
                        (JsonArray)taxonomy.Content["TaxonomyPart"]["Terms"],
                        termContentItemId);

                    terms.Add(term);
                }

                return terms;
            });

        Field<ContentItemInterface, ContentItem>("taxonomyContentItem")
            .Description(S["the taxonomy content item"])
            .ResolveLockedAsync(async context =>
            {
                var contentManager = context.RequestServices.GetService<IContentManager>();

                return await contentManager.GetAsync(context.Source.TaxonomyContentItemId);
            });
    }
}
