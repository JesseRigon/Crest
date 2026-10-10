using Crest.Security.Permissions;

namespace Crest.Queries.Sql;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        QueriesPermissions.ManageSqlQueries,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'QueriesPermissions.ManageSqlQueries'.")]
    public static readonly Permission ManageSqlQueries = QueriesPermissions.ManageSqlQueries;

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions = _allPermissions,
        },
    ];
}
