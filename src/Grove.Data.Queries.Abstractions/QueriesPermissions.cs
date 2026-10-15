using Grove.Security.Permissions;

namespace Grove.Data.Queries;

public static class QueriesPermissions
{
    public static readonly Permission ManageSqlQueries = new("ManageSqlQueries", "Manage SQL Queries", true);
}
