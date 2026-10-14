using Crest.Security.Permissions;

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

    /// <summary>A member's own Person, contact points and positions in the organizations they
    /// are bound to (docs/access.md § Members, ruling 2026-10-13). Not on the member ceiling.</summary>
    public static readonly Permission ManageOwnOrgMember = new(Crest.Access.OrganizationPermissionNames.ManageOwnOrgMember, "Manage one's own member record");

    /// <summary>An organization's members and contacts: staff at tenant reach (every
    /// organization), a member through an organization group (that organization only).</summary>
    public static readonly Permission ManageOrgMembers = new(Crest.Access.OrganizationPermissionNames.ManageOrgMembers, "Manage an organization's members and contacts");

    /// <summary>Moves an organization's ownership to another user of the tenant (docs/permissions.md
    /// › The organization owner). A tenant permission: staff only, on the member ceiling; the
    /// current owner transfers from their own account without it.</summary>
    public static readonly Permission TransferOrganizationOwnership = new("TransferOrganizationOwnership", "Transfer an organization's ownership", isSecurityCritical: true);

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
        MembersPermissions.ManageOwnOrgMember,
        MembersPermissions.ManageOrgMembers,
        MembersPermissions.TransferOrganizationOwnership,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = Crest.PlatformConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}

