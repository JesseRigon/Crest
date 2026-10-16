using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Services;
using Grove.Entities;
using Grove.Infrastructure.Settings;
using Grove.Data.Indexing;
using YesSql.Services;
using Grove.Contents.Taxonomies.Models;
using Grove.Contents.Taxonomies.Settings;
using YesSql;

namespace Grove.Contents.Taxonomies.Services;

/// <summary>
/// The content items list's taxonomy filters: one per taxonomy the taxonomy filters settings name
/// (<c>?taxonomy-{taxonomyId}=Taxonomy:{taxonomyId}</c> for the items with any of its terms,
/// <c>Term:{termId}</c> for one term's items), over the option list field index. With a content
/// type selected, only the taxonomies its fields on taxonomy terms use (<see cref="TaxonomyFields"/>).
/// </summary>
public sealed class TaxonomyContentItemListFilter(
    ISiteService siteService,
    IContentManager contentManager,
    IContentDefinitionManager contentDefinitionManager) : IContentItemListFilter
{
    private const string ParameterPrefix = "taxonomy-";

    public async Task<IReadOnlyList<ContentItemListFilterDescriptor>> DescribeAsync(string contentType, CancellationToken cancellationToken = default)
    {
        var taxonomyIds = (await siteService.GetSettingsAsync<TaxonomyContentsAdminListSettings>()).TaxonomyContentItemIds;
        if (taxonomyIds.Length > 0 && !string.IsNullOrEmpty(contentType)
            && await contentDefinitionManager.GetTypeDefinitionAsync(contentType) is { } definition)
        {
            var fieldTaxonomyIds = definition.FieldSets
                .SelectMany(part => part.FieldSetDefinition.Fields)
                .Select(TaxonomyFields.TaxonomyOf)
                .Where(taxonomyId => taxonomyId is not null);
            taxonomyIds = [.. taxonomyIds.Intersect(fieldTaxonomyIds)];
        }

        if (taxonomyIds.Length == 0)
        {
            return [];
        }

        var descriptors = new List<ContentItemListFilterDescriptor>();
        foreach (var taxonomy in await contentManager.GetAsync(taxonomyIds))
        {
            if (!taxonomy.TryGet<TaxonomyFieldSet>(out _))
            {
                continue;
            }

            var options = new List<ContentItemListFilterOption> { new("Taxonomy:" + taxonomy.ContentItemId, "Show all") };
            AddTerms(options, TaxonomyFieldSet.Terms(taxonomy), 0);
            descriptors.Add(new ContentItemListFilterDescriptor(ParameterPrefix + taxonomy.ContentItemId, taxonomy.DisplayText, options));
        }

        return descriptors;
    }

    public async Task FilterAsync(IQuery<ContentItem> query, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        foreach (var (name, value) in parameters)
        {
            if (!name.StartsWith(ParameterPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value.StartsWith("Taxonomy:", StringComparison.OrdinalIgnoreCase))
            {
                // Filed under any of the taxonomy's terms.
                var taxonomy = await contentManager.GetAsync(value[9..]);
                var termIds = taxonomy is null ? [] : TaxonomyFields.AllTerms(taxonomy).Select(term => term.ContentItemId).ToArray();
                query.All(_ => query.With<OptionListFieldIndex>(index => index.SelectedId.IsIn(termIds)));
            }
            else if (value.StartsWith("Term:", StringComparison.OrdinalIgnoreCase))
            {
                var termId = value[5..];
                query.All(_ => query.With<OptionListFieldIndex>(index => index.SelectedId == termId));
            }
        }
    }

    private static void AddTerms(List<ContentItemListFilterOption> options, IEnumerable<ContentItem> terms, int level)
    {
        foreach (var term in terms)
        {
            options.Add(new ContentItemListFilterOption("Term:" + term.ContentItemId, term.DisplayText, level));
            AddTerms(options, TaxonomyFieldSet.Terms(term), level + 1);
        }
    }
}
