using OrchardCore.Security.Permissions;

namespace Crest.Members.Permissions;

public static class MembersPermissions
{
    /// <summary>Manage member accounts, org bindings and member hierarchies (the
    /// tenant-side authority; the member admin's PORTAL surface is scoped by the
    /// hierarchy root guard, not by holding this).</summary>
    public static readonly Permission ManageMembers = new("ManageMembers", "Manage members and org bindings");

    /// <summary>Enter a member portal as a member (dual-identity session). Security
    /// critical: functional actions attribute to the member.</summary>
    public static readonly Permission ImpersonateMembers = new("ImpersonateMembers", "Impersonate members in their portal", isSecurityCritical: true);

    /// <summary>Convert an account between staff and member class. Security critical:
    /// the class decides which login surfaces accept the account.</summary>
    public static readonly Permission ConvertUserClass = new("ConvertUserClass", "Convert a user between staff and member", isSecurityCritical: true);

}

/// <summary>The Members feature's own permissions: who is a member, and staff actions
/// over them.</summary>
public sealed class MembersPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        MembersPermissions.ManageMembers,
        MembersPermissions.ImpersonateMembers,
        MembersPermissions.ConvertUserClass,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCore.OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}

