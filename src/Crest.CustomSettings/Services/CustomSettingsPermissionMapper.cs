using Crest.Access;

namespace Crest.CustomSettings.Services;

/// <summary>
/// <c>ManageResourceSettings</c> asked against a custom settings type name is also granted
/// by that type's own permission.
/// </summary>
public sealed class CustomSettingsPermissionMapper : IResourcePermissionMapper
{
    public ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (permission != "ManageResourceSettings" || resource is not string settingsType || settingsType.Length == 0)
        {
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(null);
        }

        IReadOnlyList<PermissionCandidate> candidates = [Permissions.CreatePermissionName(settingsType), permission];
        return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(candidates);
    }
}
