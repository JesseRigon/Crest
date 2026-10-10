using Crest.Security.Permissions;

namespace Crest.Taxonomies;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageTaxonomies = new("ManageTaxonomy", "Manage taxonomies");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageTaxonomies,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
