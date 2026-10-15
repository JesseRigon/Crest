namespace Grove.Access.Members.Models;

/// <summary>
/// One user's place in one organization's hierarchy (the staff hierarchy is the tenant
/// organization's): their direct managers and their computed ancestor list. A small
/// document per (user, organization); <see cref="Indexes.UserHierarchyClosureIndex"/>
/// emits one row per ancestor, which is the closure table (ruling 2026-10-06). The
/// managers are the record of truth; the ancestors are derived and rebuildable.
/// </summary>
public sealed class UserHierarchyMembership
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>Direct managers (reports-to edges). A user may have several.</summary>
    public List<string> ManagerIds { get; set; } = [];

    /// <summary>Every user above this one by any path; derived from the edges.</summary>
    public List<string> AncestorIds { get; set; } = [];
}
