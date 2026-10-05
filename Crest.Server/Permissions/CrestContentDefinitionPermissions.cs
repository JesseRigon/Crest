using OrchardCore.Security.Permissions;

namespace Crest.Permissions;

public sealed class CrestContentDefinitionPermissions : IPermissionProvider
{
    // The tenant's lever on definition locks (docs/content-items.md › Definition locks):
    // place or lift TENANT-set locks on parts, fields and attachments. Module-set locks are
    // the owning module's contract and stay fixed regardless. Deliberately NOT implied by
    // EditContentTypes - a role may shape definitions without being able to unfreeze what
    // the super admin froze.
    public static readonly Permission LockContentDefinitions = new(
        nameof(LockContentDefinitions),
        "Lock or unlock content definitions (tenant-set locks; module-set locks stay fixed)",
        isSecurityCritical: true);

    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult<IEnumerable<Permission>>([LockContentDefinitions]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new() { Name = "Administrator", Permissions = [LockContentDefinitions] },
    ];
}
