using Crest.Members.Models;

namespace Crest.Members.Services;

/// <summary>
/// One root of the shared hierarchy forest: the tenant staff tree
/// (<see cref="HierarchyRootKinds.Staff"/>, no org) or one organization's tree.
/// EVERY service call carries the root and is guarded against it — the root scope is
/// the boundary that keeps org A out of org B and members out of the staff tree
/// (design: plans/user-systems.md § Hierarchies).
/// </summary>
public sealed record HierarchyRoot(string RootKind, string? OrganizationId)
{
    public static readonly HierarchyRoot Staff = new(HierarchyRootKinds.Staff, null);

    public static HierarchyRoot ForOrganization(string organizationId)
        => new(HierarchyRootKinds.Organization, organizationId);
}

/// <summary>
/// The per-tenant user hierarchy store (custom table, adjacency + materialized path;
/// NOT content items, NOT a YesSql index). Staff and members share one forest with
/// different roots; a multi-org member has one node per org binding.
/// </summary>
public interface IUserHierarchyService
{
    /// <summary>Adds a node for a user under a parent (null parent = top level of the
    /// root). Refuses a parent that does not belong to <paramref name="root"/>.</summary>
    Task<HierarchyNodeModel> AddAsync(HierarchyRoot root, string userId, long? parentNodeId = null, CancellationToken cancellationToken = default);

    /// <summary>Moves a node (with its whole subtree) under a new parent in the SAME
    /// root. Refuses cross-root moves and moves under the node's own subtree.</summary>
    Task MoveAsync(HierarchyRoot root, long nodeId, long? newParentNodeId, CancellationToken cancellationToken = default);

    /// <summary>Removes a LEAF node. Refuses non-leaves (re-parent children first) and
    /// nodes outside the root.</summary>
    Task RemoveAsync(HierarchyRoot root, long nodeId, CancellationToken cancellationToken = default);

    /// <summary>All nodes of a root, path-ordered (a natural tree traversal).</summary>
    Task<IReadOnlyList<HierarchyNodeModel>> GetNodesAsync(HierarchyRoot root, CancellationToken cancellationToken = default);

    /// <summary>The user ids in the subtree(s) rooted at this user's node(s) within the
    /// root — "me and everyone under me", the scope resolver's expansion input. A user
    /// with no node yields exactly themself (fail-closed: no tree, no expansion).</summary>
    Task<IReadOnlyList<string>> GetSubtreeUserIdsAsync(HierarchyRoot root, string userId, CancellationToken cancellationToken = default);

    /// <summary>The chain of user ids from the root down to this user (escalation /
    /// approval routing). Empty when the user has no node in the root.</summary>
    Task<IReadOnlyList<string>> GetChainUserIdsAsync(HierarchyRoot root, string userId, CancellationToken cancellationToken = default);

    /// <summary>Every node any root holds for this user (a member's nodes across their
    /// org bindings, or a staff node). Used by class conversion and cleanup.</summary>
    Task<IReadOnlyList<HierarchyNodeModel>> GetNodesForUserAsync(string userId, CancellationToken cancellationToken = default);
}
