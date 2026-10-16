using Grove.Security.Permissions;

namespace Grove.Permissions;

public sealed class OptionListPermissions : IPermissionProvider
{
    public static readonly Permission ViewOptionLists = new(nameof(ViewOptionLists), "View option lists");

    public static readonly Permission ManageOptionLists = new(nameof(ManageOptionLists), "Manage option lists", [ViewOptionLists], isSecurityCritical: true);

    // The tenant super admin's lever: place or lift TENANT-set locks on option lists.
    // Module-set locks are a developer contract and stay immutable regardless of this
    // permission. Deliberately NOT implied by ManageOptionLists - a role may curate
    // lists without being able to unfreeze what the super admin froze.
    public static readonly Permission LockOptionLists = new(
        nameof(LockOptionLists),
        "Lock or unlock option lists (tenant-set locks; module-set locks stay fixed)",
        isSecurityCritical: true);

    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult<IEnumerable<Permission>>([ViewOptionLists, ManageOptionLists, LockOptionLists]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new()
        {
            Name = "Administrator",
            Permissions = [ManageOptionLists, LockOptionLists],
        },
        new()
        {
            Name = "Editor",
            Permissions = [ViewOptionLists],
        },
    ];
}
