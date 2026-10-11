using Crest.Access;
using Microsoft.Extensions.Options;

namespace Crest.Settings.Services;

/// <summary>
/// <c>ManageGroupSettings</c> asked against a settings group id is also granted by any
/// permission a module registered for that group.
/// </summary>
public sealed class SiteSettingsPermissionMapper(IOptions<SiteSettingsPermissionOptions> options) : IResourcePermissionMapper
{
    public ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (permission != SettingsPermissions.ManageGroupSettings.Name
            || resource is not string groupId
            || string.IsNullOrEmpty(groupId)
            || !options.Value.GroupPermissions.TryGetValue(groupId, out var permissions))
        {
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(null);
        }

        var candidates = new List<PermissionCandidate> { permission };
        candidates.AddRange(permissions.Select(p => new PermissionCandidate(p.Name)));
        return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(candidates);
    }
}
