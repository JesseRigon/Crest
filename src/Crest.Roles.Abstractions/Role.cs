using Crest.Security.Permissions;

namespace Crest.Security;

public class Role : IRole
{
    public string RoleName { get; set; }

    public string RoleDescription { get; set; }

    public string NormalizedRoleName { get; set; }

    /// <summary>The loose permissions the role grants directly (<see cref="Permission.ClaimType"/> claims).</summary>
    public List<RoleClaim> RoleClaims { get; set; } = [];

    /// <summary>
    /// The permission sets the role holds, by name (docs/permissions.md, the permission model):
    /// a tenant-defined named list of permissions, expanded with the loose claims when the
    /// role's permissions are read.
    /// </summary>
    public List<string> PermissionSets { get; set; } = [];

    public Role Clone()
    {
        var role = new Role
        {
            RoleName = RoleName,
            RoleDescription = RoleDescription,
            NormalizedRoleName = NormalizedRoleName,
            PermissionSets = [.. PermissionSets],
        };

        foreach (var claim in RoleClaims)
        {
            role.RoleClaims.Add(claim.Clone());
        }

        return role;
    }
}
