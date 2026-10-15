using Grove.Indexing;
using Grove.Security.Permissions;

namespace Grove.Elasticsearch;

public static class ElasticsearchPermissions
{
    public static readonly Permission ManageElasticIndexes = new("ManageElasticIndexes", "Manage Elasticsearch Indexes", [IndexingPermissions.ManageIndexes]);

    public static readonly Permission QueryElasticApi = new("QueryElasticsearchApi", "Query Elasticsearch Api", [ManageElasticIndexes]);
}
