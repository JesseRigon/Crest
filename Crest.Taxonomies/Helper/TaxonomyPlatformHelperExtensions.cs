using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.ContentManagement;
using Crest.Taxonomies.Indexing;
using YesSql;

#pragma warning disable CA1050 // Declare types in namespaces
public static class TaxonomyPlatformHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Returns a term from its content item id and taxonomy.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="taxonomyContentItemId">The taxonomy content item id.</param>
    /// <param name="termContentItemId">The term content item id.</param>
    /// <returns>A content item id <c>null</c> if it was not found.</returns>
    public static async Task<ContentItem> GetTaxonomyTermAsync(this IPlatformHelper platformHelper, string taxonomyContentItemId, string termContentItemId)
    {
        var contentManager = platformHelper.HttpContext.RequestServices.GetService<IContentManager>();
        var taxonomy = await contentManager.GetAsync(taxonomyContentItemId);

        if (taxonomy == null)
        {
            return null;
        }

        return FindTerm((JsonArray)taxonomy.Content.TaxonomyPart.Terms, termContentItemId);
    }

    /// <summary>
    /// Returns the list of terms including their parents.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="taxonomyContentItemId">The taxonomy content item id.</param>
    /// <param name="termContentItemId">The term content item id.</param>
    /// <returns>A list content items.</returns>
    public static async Task<List<ContentItem>> GetInheritedTermsAsync(this IPlatformHelper platformHelper, string taxonomyContentItemId, string termContentItemId)
    {
        var contentManager = platformHelper.HttpContext.RequestServices.GetService<IContentManager>();
        var taxonomy = await contentManager.GetAsync(taxonomyContentItemId);

        if (taxonomy == null)
        {
            return null;
        }

        var terms = new List<ContentItem>();

        FindTermHierarchy((JsonArray)taxonomy.Content.TaxonomyPart.Terms, termContentItemId, terms);

        return terms;
    }

    /// <summary>
    /// Query content items.
    /// </summary>
    public static async Task<IEnumerable<ContentItem>> QueryCategorizedContentItemsAsync(this IPlatformHelper platformHelper, Func<IQuery<ContentItem, TaxonomyIndex>, IQuery<ContentItem>> query)
    {
        var contentManager = platformHelper.HttpContext.RequestServices.GetService<IContentManager>();
        var session = platformHelper.HttpContext.RequestServices.GetService<ISession>();

        var contentItems = await query(session.Query<ContentItem, TaxonomyIndex>()).ListAsync();

        return await contentManager.LoadAsync(contentItems);
    }

    internal static ContentItem FindTerm(JsonArray termsArray, string termContentItemId)
    {
        foreach (var term in termsArray.Cast<JsonObject>())
        {
            var contentItemId = term["ContentItemId"]?.ToString();

            if (contentItemId == termContentItemId)
            {
                return term.ToObject<ContentItem>();
            }

            if (term["Terms"] is JsonArray children)
            {
                var found = FindTerm(children, termContentItemId);

                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    internal static bool FindTermHierarchy(JsonArray termsArray, string termContentItemId, List<ContentItem> terms)
    {
        foreach (var term in termsArray.Cast<JsonObject>())
        {
            var contentItemId = term["ContentItemId"]?.ToString();

            if (contentItemId == termContentItemId)
            {
                terms.Add(term.ToObject<ContentItem>());

                return true;
            }

            if (term["Terms"] is JsonArray children)
            {
                var found = FindTermHierarchy(children, termContentItemId, terms);

                if (found)
                {
                    terms.Add(term.ToObject<ContentItem>());

                    return true;
                }
            }
        }

        return false;
    }
}
