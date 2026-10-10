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
/// Maps a permission asked against a resource to the permission that actually decides it:
/// a content item maps <c>ViewContent</c> to <c>View_{type}</c> or, for the owner,
/// <c>ViewOwn_{type}</c>. Registered by the module that owns the resource kind.
/// </summary>
public interface IResourcePermissionMapper
{
    /// <returns>The permission names any of which grant the request, or null when this
    /// mapper does not know the resource.</returns>
    IReadOnlyList<string>? Map(string permission, object resource, CallerContext caller);
}

/// <summary>
/// A deny-only rule evaluated before any grant: permissions a caller class can never hold
/// (the member class ceiling), or a policy that narrows a caller. A single deny wins.
/// </summary>
public interface IAccessCeiling
{
    /// <returns>A reason when the permission is ceilinged for this caller; null otherwise.</returns>
    string? Deny(CallerContext caller, string permission);
}
