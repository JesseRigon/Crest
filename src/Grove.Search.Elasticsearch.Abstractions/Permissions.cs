using Grove.Security.Permissions;

namespace Grove.Search.Elasticsearch;

public static class Permissions
{
    public static readonly Permission ManageElasticIndexes = new("ManageElasticIndexes", "Manage Elasticsearch Indexes");
}
