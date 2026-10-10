using Crest.Security.Permissions;

namespace Crest.Queries;

public static class QueriesPermissions
{
    public static readonly Permission ManageSqlQueries = new("ManageSqlQueries", "Manage SQL Queries", true);
}
