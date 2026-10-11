namespace Crest.Access;

public enum AccessVerdict
{
    Allow,
    Deny,
    /// <summary>Denied, and the caller must not learn that the resource exists.</summary>
    NotFound,
}

/// <param name="IsFinal">A ceiling denial: no other grant may override it.</param>
public sealed record AccessDecision(AccessVerdict Verdict, string? Reason = null, bool IsFinal = false)
{
    public bool IsAllowed => Verdict == AccessVerdict.Allow;

    public static readonly AccessDecision Allowed = new(AccessVerdict.Allow);

    public static AccessDecision Denied(string reason) => new(AccessVerdict.Deny, reason);

    public static AccessDecision Ceilinged(string reason) => new(AccessVerdict.Deny, reason, IsFinal: true);
}

/// <summary>
/// The one answer to "may this caller do this": the super user, the Anonymous and
/// Authenticated roles, the caller's own roles with implied permissions, resource mapping
/// (a content item's per-type and owner variations), the class ceiling and the access
/// policies, in one implementation. No surface decides on its own.
/// </summary>
public interface IAccessDecision
{
    /// <param name="permission">The permission name.</param>
    /// <param name="resource">The resource the permission is asked against, when
    /// resource-bound (a content item, a content type name, a role, ...).</param>
    Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object? resource = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// One way a resource-bound request can be granted: a permission name the caller may hold
/// directly, or - with <see cref="Resource"/> set - the same question re-asked for another
/// resource (a media folder's content item, a folder's view right). <see cref="Granted"/> is
/// the mapper's way to say the resource is public for this permission.
/// </summary>
public readonly record struct PermissionCandidate(string Permission, object? Resource = null)
{
    public const string GrantedPermission = "*";

    /// <summary>The resource needs no permission for this request (public media, for one).</summary>
    public static readonly PermissionCandidate Granted = new(GrantedPermission);

    public static implicit operator PermissionCandidate(string permission) => new(permission);
}

/// <summary>
/// Maps a permission asked against a resource to what actually decides it: a content item
/// maps <c>ViewContent</c> to <c>View_{type}</c> or, for the owner, <c>ViewOwn_{type}</c>; a
/// media folder maps <c>ViewMedia</c> to the folder's own right or to the content item the
/// folder belongs to. Any candidate grants. Registered by the module that owns the resource
/// kind; the one decision runs them in place of per-module authorization handlers.
/// </summary>
public interface IResourcePermissionMapper
{
    /// <returns>The candidates any of which grant the request (an empty list denies), or
    /// null when this mapper does not know the resource.</returns>
    ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default);
}

/// <summary>
/// A deny-only rule evaluated before any grant: permissions a caller class can never hold
/// (the member class ceiling), or a policy that narrows a caller. A single deny wins.
/// </summary>
public interface IAccessCeiling
{
    /// <returns>A reason when the permission is ceilinged for this caller, on this resource
    /// when one is asked against; null otherwise.</returns>
    string? Deny(CallerContext caller, string permission, object? resource = null);
}
