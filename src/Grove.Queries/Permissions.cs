using Grove.Security.Permissions;

namespace Grove.Queries;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageQueries = QueryPermissions.ManageQueries;
    public static readonly Permission ExecuteApiAll = QueryPermissions.ExecuteApiAll;

    private readonly IQueryManager _queryManager;
    private readonly IEnumerable<Permission> _generalPermissions =
    [
        ManageQueries,
    ];

    public Permissions(IQueryManager queryManager)
    {
        _queryManager = queryManager;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var list = new List<Permission>()
        {
            ManageQueries,
            ExecuteApiAll,
        };

        var queries = await _queryManager.ListQueriesAsync(true);

        foreach (var query in queries)
        {
            list.Add(CreatePermissionForQuery(query.Name));
        }

        return list;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = _generalPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions = _generalPermissions,
        },

        // The site query's anonymous subset (the site name and the branding renditions) is
        // what the login page renders (docs/branding.md › Permissions).
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Anonymous,
            Permissions = [CreatePermissionForQuery(Builtin.SystemQuerySource.SiteQuery)],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Authenticated,
            Permissions = [CreatePermissionForQuery(Builtin.SystemQuerySource.SiteQuery)],
        },
    ];

    public static Permission CreatePermissionForQuery(string name)
        => QueryPermissions.CreatePermissionForQuery(name);
}
