using Grove.Access.Members.Models;

namespace Grove.Access.Members.Services;

/// <summary>
/// The per-tenant reports-to hierarchy (rulings 2026-10-05, 2026-10-06): manager → report
/// relationships, not a tree of slots. A user may have several managers, a manager many
/// reports. One root per organization; the staff hierarchy is the tenant organization's.
/// EVERY call carries the organization and is guarded by it, so organization A's
/// hierarchy never reaches organization B's. Built on YesSql as a closure table
/// (<see cref="UserHierarchyMembership"/> documents and their closure index).
/// </summary>
public interface IUserHierarchyService
{
    /// <summary>Gives the user a node in the organization (no managers). Idempotent.</summary>
    Task EnsureNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Removes the user's node; their reports lose that manager and their
    /// ancestor lists are recomputed.</summary>
    Task RemoveNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Adds a reports-to edge. Refused when either user has no node in the
    /// organization or when the report is already above the manager (a cycle).</summary>
    Task AddManagerAsync(string organizationId, string reportId, string managerId, CancellationToken cancellationToken = default);

    Task RemoveManagerAsync(string organizationId, string reportId, string managerId, CancellationToken cancellationToken = default);

    /// <summary>Every node of the organization.</summary>
    Task<IReadOnlyList<HierarchyNodeModel>> GetNodesAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>"Me and everyone under me" in the organization: the scope expansion
    /// input. A user with no node yields exactly themself (no tree, no expansion).</summary>
    Task<IReadOnlyList<string>> GetSubtreeUserIdsAsync(string organizationId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Every path from a top down to the user (escalation and approval routing).
    /// Empty when the user has no node in the organization.</summary>
    Task<IReadOnlyList<IReadOnlyList<string>>> GetChainsAsync(string organizationId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Every node any organization holds for this user.</summary>
    Task<IReadOnlyList<HierarchyNodeModel>> GetNodesForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Recomputes every ancestor list of the organization from its edges and drops
    /// edges to users without a node: the repair path.</summary>
    Task RebuildAsync(string organizationId, CancellationToken cancellationToken = default);
}
