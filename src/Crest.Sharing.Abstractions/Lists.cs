using Crest.Access;

namespace Crest.Sharing;

/// <summary>
/// Implemented, optionally, by an <see cref="IShareableKind"/> whose objects have children
/// (docs/sharing.md, requirement 78): a change to an object's entries or rules recalculates the
/// object and everything below it, and this is how sharing finds what is below. A kind without
/// a tree (its objects never parent anything) does not implement it; a kind whose objects can
/// be parents must, or a change on a parent leaves its children's access index stale until
/// they are recalculated for another reason.
/// </summary>
public interface IShareableTree
{
    /// <summary>The direct children of the object (of any kind: a drive's folders, a folder's
    /// files). Empty for a leaf or an unknown object. Grandchildren are asked for in turn.</summary>
    Task<IReadOnlyList<ShareableRef>> ChildrenAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where a kind's objects are rows: the index table a list or search reads, and its column that
/// holds the object id (the id <see cref="IShareableKind.TryIdentify"/> returns). Sharing
/// registers an <c>IScopeProvider</c> for <see cref="Table"/>, so a list of that table admits
/// only the rows whose object the caller may view (requirement 77).
/// </summary>
/// <param name="Table">The index table name, without the tenant's table prefix
/// (<c>MediaPlacementIndex</c>).</param>
/// <param name="IdColumn">The column holding the object id, compared as a string.</param>
public sealed record ShareScopeTarget(string Table, string IdColumn);

/// <summary>
/// Implemented, optionally, by an <see cref="IShareableKind"/> whose objects live in an index
/// table: sharing then filters that table's lists and searches in the query. A kind without
/// one is decided object by object (the mapper) only.
/// </summary>
public interface IShareableScopeTarget
{
    ShareScopeTarget ScopeTarget { get; }
}

/// <summary>
/// The access index (docs/sharing.md, requirements 75-82): each object's precomputed access,
/// which lists and searches filter on. Sharing keeps it current for its own changes (entries
/// and rules). The module that owns a kind tells it when something else changes an object's
/// access (requirement 78): an object created, moved or deleted, or a fact a rule reads
/// changed. An object never calculated has no index rows and is in no list (82).
/// </summary>
public interface IShareAccessIndex
{
    /// <summary>
    /// Recalculates the object, and everything below it when <paramref name="includeDescendants"/>
    /// (a move, or a new parent). Applied before it returns, so it may also remove access
    /// (requirement 80). Call it after the object is saved where its kind reads it.
    /// </summary>
    Task RecalculateAsync(ShareableRef target, bool includeDescendants = false, CancellationToken cancellationToken = default);

    /// <summary>Removes the object's rows (it was deleted), and its descendants' when
    /// <paramref name="includeDescendants"/> and they are still enumerable.</summary>
    Task RemoveAsync(ShareableRef target, bool includeDescendants = false, CancellationToken cancellationToken = default);

    /// <summary>Queues a rebuild of the whole index: every root of every kind that lists its
    /// roots (<see cref="IShareableRoots"/>) and everything below them, in the background.
    /// Sharing queues one itself when the feature is enabled.</summary>
    Task QueueRebuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implemented, optionally, by an <see cref="IShareableKind"/> so the access index can be
/// rebuilt from nothing (sharing turned on over existing objects): the kind's objects that
/// have no parent. A kind whose objects all have parents of another kind (content items under
/// content types) need not implement it; a flat kind returns every object.
/// </summary>
public interface IShareableRoots
{
    Task<IReadOnlyList<ShareableRef>> RootsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implemented, optionally, by an <see cref="IShareableKind"/> whose roots have administrators
/// assigned by the tenant rather than by each other (media requirement 31: shared drives): a
/// holder of <see cref="AdminAssignmentPermission"/> may add and remove
/// <see cref="AccessLevel.Admin"/> grants on the kind's roots without being an Admin there. It
/// gives nothing else: other entries, rules, and Admin anywhere but a root still take the
/// tree's Admin. Every such change is audited as made through the permission.
/// </summary>
public interface IShareableAdminAssignment
{
    /// <summary>The tenant permission's name (<c>ManageSharedDrives</c>).</summary>
    string AdminAssignmentPermission { get; }

    /// <summary>Whether the permission assigns Admins on this root (media: shared drives only,
    /// never a personal drive, whose access by administrators is a separate, logged step).
    /// Every root by default.</summary>
    bool AppliesTo(ShareableObject root) => true;
}

/// <summary>
/// Implemented, optionally, by an <see cref="IShareableKind"/> that limits who may hold
/// <see cref="AccessLevel.Admin"/> on a root (media requirement 19: on a personal drive, only its
/// owner). The sharing service refuses an Admin grant to anyone else, whoever asks, and the
/// resolver counts such a grant, stored or contributed, as Manager at most.
/// </summary>
public interface IShareableAdminEligibility
{
    bool MayHoldAdmin(ShareableObject root, SharePrincipal principal);
}

/// <summary>
/// Sharing's list filter for one kind, for a module that combines it with its own list scope
/// (Contents, which limits drafts to those who may edit): the rows of the kind's scope target
/// on which the caller's level is at least <c>minimum</c>.
/// </summary>
public interface IShareScopes
{
    /// <summary>Null when sharing does not govern the kind's lists (the kind is not registered,
    /// is in conflict, or has no <see cref="IShareableScopeTarget"/>).</summary>
    ScopeRule? RuleFor(CallerContext caller, string kind, AccessLevel minimum);
}
