using Grove.Elasticsearch.Models;
using Grove.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Elasticsearch;

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

        yield return PageEntries.Legacy("elasticsearchquery", "Run Elasticsearch Query", "queries", "Grove.Elasticsearch", "Admin", "Query", [ElasticsearchPermissions.ManageElasticIndexes.Name]);
    }
}
