using Grove.Data.Indexing;
using Grove.Security.Permissions;

namespace Grove.Search.Elasticsearch;

public static class ElasticsearchPermissions
{
    public static readonly Permission ManageElasticIndexes = new("ManageElasticIndexes", "Manage Elasticsearch Indexes", [IndexingPermissions.ManageIndexes]);
}
