using Crest.Elasticsearch.Models;
using Crest.Routing;
using Microsoft.Extensions.Options;

namespace Crest.Elasticsearch;

/// <summary>
/// The Elasticsearch query runner, while a connection is configured: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages(IOptions<ElasticsearchConnectionOptions> connectionOptions) : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        if (!connectionOptions.Value.ConfigurationExists())
        {
            yield break;
        }

        yield return PageEntries.Legacy("elasticsearchquery", "Run Elasticsearch Query", "queries", "Crest.Elasticsearch", "Admin", "Query", [ElasticsearchPermissions.ManageElasticIndexes.Name]);
    }
}
