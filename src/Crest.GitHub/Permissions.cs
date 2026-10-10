using Crest.Security.Permissions;

namespace Crest.GitHub;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageGitHubAuthentication = new("ManageGitHubAuthentication", "Manage GitHub Authentication settings");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageGitHubAuthentication,
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
