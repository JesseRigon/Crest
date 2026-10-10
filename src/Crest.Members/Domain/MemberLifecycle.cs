namespace Crest.Members.Models;

/// <summary>
/// What happened to a member's relationship with one organization. Members raises these;
/// downstream modules that keep their own records keyed by (member, organization) handle
/// them and clean up or seed accordingly.
/// </summary>
/// <remarks>
/// The seam exists because Members must not know its consumers. A subscription module is
/// the typical one: where a seat is keyed by (portal user, organization), leaving an
/// organization ends everything keyed to that pair, and becoming an organization's first
/// admin is when that organization's groups are first worth creating. Members itself has
/// no notion of any of that.
/// </remarks>
public sealed record MemberBoundContext(string UserId, string OrganizationId, bool IsFirstOrganizationAdmin);

/// <inheritdoc cref="MemberBoundContext"/>
public sealed record MemberUnboundContext(string UserId, string OrganizationId);

/// <summary>
/// Handles a member's organization-binding lifecycle. Implementations are resolved in a
/// FRESH scope after the binding write has committed (Members registers a deferred task),
/// so a handler may use the content manager freely — it is never re-entering the session
/// that made the change. A handler must be idempotent: the same binding can be reported
/// again if a member leaves an organization and rejoins it.
/// </summary>
public interface IMemberLifecycleHandler
{
    /// <summary>A member was bound to an organization.</summary>
    Task MemberBoundAsync(MemberBoundContext context, CancellationToken cancellationToken = default);

    /// <summary>A member's binding to an organization was removed.</summary>
    Task MemberUnboundAsync(MemberUnboundContext context, CancellationToken cancellationToken = default);
}
