using System.Text.Json.Nodes;
using Crest;
using Crest.ContentManagement;
using Crest.Queries;

#pragma warning disable CA1050 // Declare types in namespaces
public static class ContentQueryPlatformRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    public static Task<IEnumerable<ContentItem>> ContentQueryAsync(this IPlatformHelper platformHelper, string queryName)
    {
        return ContentQueryAsync(platformHelper, queryName, new Dictionary<string, object>());
    }

    public static async Task<IEnumerable<ContentItem>> ContentQueryAsync(this IPlatformHelper platformHelper, string queryName, IDictionary<string, object> parameters)
    {
        var results = await platformHelper.QueryAsync(queryName, parameters);
        var contentItems = new List<ContentItem>();

        if (results != null)
        {
            foreach (var result in results)
            {
                if (result is not ContentItem contentItem)
                {
                    contentItem = null;

                    if (result is JsonObject jObject)
                    {
                        contentItem = jObject.ToObject<ContentItem>();
                    }
                }

                // If input is a 'JObject' but which not represents a 'ContentItem',
                // a 'ContentItem' is still created but with some null properties.
                if (contentItem?.ContentItemId == null)
                {
                    continue;
                }

                contentItems.Add(contentItem);
            }
        }

        return contentItems;
    }

    public static async Task<IQueryResults> ContentQueryResultsAsync(this IPlatformHelper platformHelper, string queryName, Dictionary<string, object> parameters)
    {
        var contentItems = new List<ContentItem>();
        var queryResult = await platformHelper.QueryResultsAsync(queryName, parameters);

        if (queryResult.Items != null)
        {
            foreach (var item in queryResult.Items)
            {
                if (item is not ContentItem contentItem)
                {
                    contentItem = null;

                    if (item is JsonObject jObject)
                    {
                        contentItem = jObject.ToObject<ContentItem>();
                    }
                }

                // If input is a 'JObject' but which not represents a 'ContentItem',
                // a 'ContentItem' is still created but with some null properties.
                if (contentItem?.ContentItemId == null)
                {
                    continue;
                }

                contentItems.Add(contentItem);
            }

            queryResult.Items = contentItems;
        }

        return queryResult;
    }
}
