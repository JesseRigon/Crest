namespace Grove.Sharing;

/// <summary>
/// One object's precomputed access (docs/sharing.md, requirement 75; decision F3): for each
/// principal with a decision, the decision that wins among that principal's own (by depth, then
/// order), over time. Every object that was calculated has one; an object with none is in no
/// list (requirement 82). Lists and search filter on its <c>ShareAccessIndex</c> rows; the
/// decision for one object is still resolved live (the mapper), and both come from the same
/// entries and rules (requirement 81).
/// </summary>
public sealed class ShareAccessDocument
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    /// <summary>Which path to the root this document is for (multi-path inheritance, F3):
    /// empty for the primary path, else <see cref="Services.SharingResolver.PathKey"/>. An object
    /// reached through several paths has a document per path, and lists admit it when any of
    /// them does.</summary>
    public string PathKey { get; set; } = string.Empty;

    /// <summary>What list scopes correlate one path's rows by: the object id, followed by
    /// <c>~</c> and the path key for a path other than the primary one.</summary>
    public string AccessKey => KeyFor(ObjectId, PathKey);

    public static string KeyFor(string objectId, string pathKey) => pathKey.Length == 0 ? objectId : $"{objectId}~{pathKey}";

    /// <summary>The Anyone tier grants now (when it was calculated); the rows carry the exact
    /// window.</summary>
    public bool IsPublic { get; set; }

    /// <summary>Someone other than the owner may view it now through a grant other than
    /// Anyone (when it was calculated): the "shared" access indicator (requirement 98).</summary>
    public bool IsShared { get; set; }

    /// <summary>Out of use: no rows, nobody but a tenant administrator lists it.</summary>
    public bool IsInactive { get; set; }

    /// <summary>The next time a decision on the object's chain starts or ends, when the job
    /// recalculates it (the time registry, requirement 79). Null for none.</summary>
    public DateTime? NextBoundaryUtc { get; set; }

    public DateTime CalculatedUtc { get; set; }

    public List<ShareAccessRow> Rows { get; set; } = [];

    /// <summary>Where a role's denial is deeper than another role's grant on this object, so a
    /// caller holding both is denied (requirement 60): the list scope's role tier reads these.</summary>
    public List<ShareRoleShadow> Shadows { get; set; } = [];

    public ShareableRef Ref => new(Kind, ObjectId);
}

/// <summary>
/// One principal's winning decision on an object over one window of time. A principal whose
/// winner changes over time (an entry that starts or ends) has a row per window, so a read at
/// any instant picks the right one without waiting for a recalculation.
/// </summary>
public sealed class ShareAccessRow
{
    /// <summary>The principal's tier is its kind (requirement 59).</summary>
    public SharePrincipalKind PrincipalKind { get; set; }

    /// <summary>The principal's id as entered; empty for Anyone and Tenant.</summary>
    public string PrincipalId { get; set; } = string.Empty;

    /// <summary>The organization an organization-scoped role principal is limited to.</summary>
    public string? OrganizationId { get; set; }

    /// <summary>The principal the row is about.</summary>
    public SharePrincipal Principal => new(
        PrincipalKind,
        PrincipalKind is SharePrincipalKind.Anyone or SharePrincipalKind.Tenant ? null : PrincipalId,
        OrganizationId);

    /// <summary>The depth of the winning decision: 0 the tenant, 1 the root, and so on.</summary>
    public int Depth { get; set; }

    /// <summary>The level granted; <see cref="AccessLevel.None"/> for a block.</summary>
    public AccessLevel Level { get; set; }

    public bool IsBlock { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? EndsUtc { get; set; }

    /// <summary>The winning decision is on the object itself, not inherited ("shared with me").</summary>
    public bool IsDirect { get; set; }

    /// <summary>The row stands for ownership (the object's owner, or the root's), not a decision.</summary>
    public bool IsOwner { get; set; }

    public string? RuleId { get; set; }

    public string? Source { get; set; }

    /// <summary>The row admits viewing: a grant of at least Viewer.</summary>
    public bool Admits => !IsBlock && Level >= AccessLevel.Viewer;

    public bool IsActiveAt(DateTime utc) => (StartsUtc is null || StartsUtc <= utc) && (EndsUtc is null || utc < EndsUtc);
}

/// <summary>
/// A role's row (at <see cref="Level"/>) deeper than a role grant of a higher level at
/// <see cref="Depth"/>, over a window: for a caller holding the role, those grants count only
/// up to <see cref="Level"/> (the deepest decision wins within the tier, requirement 60).
/// </summary>
public sealed class ShareRoleShadow
{
    public string Role { get; set; } = string.Empty;

    /// <summary>The organization of an organization-scoped role.</summary>
    public string? OrganizationId { get; set; }

    /// <summary>The depth of the grants this row outranks.</summary>
    public int Depth { get; set; }

    /// <summary>The level of the deeper row; <see cref="AccessLevel.None"/> for a block.</summary>
    public AccessLevel Level { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? EndsUtc { get; set; }
}

/// <summary>A subtree whose recalculation was left to the background (a change that only adds
/// access to many objects, requirement 80).</summary>
public sealed class ShareRecalculation
{
    public string Kind { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public DateTime RequestedUtc { get; set; }

    public ShareableRef Ref => new(Kind, ObjectId);
}
