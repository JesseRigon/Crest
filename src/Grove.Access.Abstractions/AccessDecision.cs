namespace Grove.Access;

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
/// A module's own mapper (neither authoritative nor additive): maps a permission asked against
/// a resource to what actually decides it, when no authority claims the resource; among
/// several, the highest-priority feature's that answers wins. Maps a permission asked against
/// a resource to what actually decides it: a content item
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
/// A mapper that <b>replaces</b> every other answer for the resources it claims (a domain
/// that takes over a kind of resource: sharing, for a kind registered as shareable). Among the
/// authoritative mappers that claim a resource, the one shipped by the feature with the
/// highest priority (the manifest's <c>Priority</c>) is the only answer, so a module - a third
/// party's included - overrides another's authority for exactly the resources it claims by
/// declaring a higher priority; for the resources it does not claim the lower one still
/// answers. Two claimants at the same priority are a configuration conflict: the decision
/// denies (finally) and says so, rather than letting registration order pick.
/// </summary>
public interface IAuthoritativeResourcePermissionMapper : IResourcePermissionMapper
{
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

/// <summary>An operation refused by the decision: no caller, or the caller may not.</summary>
public sealed class AccessDeniedException(string permission, string message) : UnauthorizedAccessException(message)
{
    public string Permission { get; } = permission;
}

public static class AccessDecisionExtensions
{
    /// <summary>
    /// The decision for an operation that must not run otherwise (a workflow activity's write,
    /// a service call without a surface of its own): throws <see cref="AccessDeniedException"/>
    /// when there is no caller or the caller may not. Allows are the operation's to record;
    /// the decision records the denial.
    /// </summary>
    public static async Task RequireAsync(this IAccessDecision decision, CallerContext? caller, string permission, object? resource = null, CancellationToken cancellationToken = default)
    {
        if (caller is null)
        {
            throw new AccessDeniedException(permission, $"'{permission}' was asked with no caller: the access gate did not run.");
        }

        var verdict = await decision.DecideAsync(caller, permission, resource, cancellationToken);
        if (!verdict.IsAllowed)
        {
            throw new AccessDeniedException(permission, $"'{caller.UserName ?? (caller.IsSystem ? "system" : "anonymous")}' may not {permission}{(resource is null ? string.Empty : $" on {resource}")}{(verdict.Reason is null ? string.Empty : $": {verdict.Reason}")}.");
        }
    }

    /// <summary>The decision for a read: false when there is no caller or the caller may not.</summary>
    public static async Task<bool> AllowsAsync(this IAccessDecision decision, CallerContext? caller, string permission, object? resource = null, CancellationToken cancellationToken = default) =>
        caller is not null && (await decision.DecideAsync(caller, permission, resource, cancellationToken)).IsAllowed;
}
