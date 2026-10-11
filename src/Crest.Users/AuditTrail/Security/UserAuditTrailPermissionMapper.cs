using Crest.Access;
using Crest.Users.AuditTrail.Models;

namespace Crest.Users.AuditTrail.Security;

/// <summary>
/// A user audit-trail event about the caller is granted by <c>ViewOwnUserAuditTrailEvents</c>
/// as well as by the tenant-wide permission.
/// </summary>
public sealed class UserAuditTrailPermissionMapper : IResourcePermissionMapper
{
    public ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (resource is not AuditTrailUserEvent userEvent
            || (permission != Permissions.ViewOwnUserAuditTrailEvents.Name && permission != Permissions.ViewUserAuditTrailEvents.Name))
        {
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(null);
        }

        var own = caller.UserId is not null && string.Equals(caller.UserId, userEvent.UserId, StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<PermissionCandidate> candidates = own
            ? [Permissions.ViewOwnUserAuditTrailEvents.Name, Permissions.ViewUserAuditTrailEvents.Name]
            : [Permissions.ViewUserAuditTrailEvents.Name];
        return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(candidates);
    }
}
