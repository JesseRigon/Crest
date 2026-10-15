using System.Text.Json.Nodes;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.Contents.Services;
using Grove.Entities;
using Grove.Settings;
using Grove.Taxonomies.Fields;
using Grove.Taxonomies.Indexing;
using Grove.Taxonomies.Models;
using Grove.Taxonomies.Settings;
using YesSql;

namespace Grove.Taxonomies.Services;

/// <summary>
/// The content items list's taxonomy filters: one per taxonomy the taxonomy filters settings name
/// (<c>?taxonomy-{taxonomyId}=Taxonomy:{taxonomyId}</c> for the items with any of its terms,
/// <c>Term:{termId}</c> for one term's items). With a content type selected, only the taxonomies
/// its taxonomy fields use.
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
            var fieldTaxonomyIds = definition.Parts
                .SelectMany(part => part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == nameof(TaxonomyField)))
                .Select(field => field.GetSettings<TaxonomyFieldSettings>().TaxonomyContentItemId);
            taxonomyIds = [.. taxonomyIds.Intersect(fieldTaxonomyIds)];
        }

        if (taxonomyIds.Length == 0)
        {
            return [];
        }

        var descriptors = new List<ContentItemListFilterDescriptor>();
        foreach (var taxonomy in await contentManager.GetAsync(taxonomyIds))
        {
            if (!taxonomy.TryGet<TaxonomyPart>(out var taxonomyPart))
            {
                continue;
            }

            var options = new List<ContentItemListFilterOption> { new("Taxonomy:" + taxonomy.ContentItemId, "Show all") };
            AddTerms(options, taxonomyPart.Terms, 0);
            descriptors.Add(new ContentItemListFilterDescriptor(ParameterPrefix + taxonomy.ContentItemId, taxonomy.DisplayText, options));
        }

        return descriptors;
    }

    public Task FilterAsync(IQuery<ContentItem> query, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        foreach (var (name, value) in parameters)
        {
            if (!name.StartsWith(ParameterPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value.StartsWith("Taxonomy:", StringComparison.OrdinalIgnoreCase))
            {
                var taxonomyId = value[9..];
                query.All(_ => query.With<TaxonomyIndex>(index => index.TaxonomyContentItemId == taxonomyId));
            }
            else if (value.StartsWith("Term:", StringComparison.OrdinalIgnoreCase))
            {
                var termId = value[5..];
                query.All(_ => query.With<TaxonomyIndex>(index => index.TermContentItemId == termId));
            }
        }

        return Task.CompletedTask;
    }

    private static void AddTerms(List<ContentItemListFilterOption> options, IEnumerable<ContentItem> terms, int level)
    {
        foreach (var term in terms)
        {
            options.Add(new ContentItemListFilterOption("Term:" + term.ContentItemId, term.DisplayText, level));
            if (((JsonObject)term.Content)["Terms"] is JsonArray children)
            {
                AddTerms(options, children.ToObject<ContentItem[]>(), level + 1);
            }
        }
    }
}
