namespace Grove.Access.Sharing;

/// <summary>
/// Who an entry is about, from the most general to the most specific. The order is the
/// resolution's principal tiers (docs/sharing.md, requirement 59): the most specific tier with
/// any decision for a person decides.
/// </summary>
public enum SharePrincipalKind
{
    /// <summary>Everyone, signed in or not: public. Only ever a Viewer.</summary>
    Anyone = 0,

    /// <summary>Everyone signed in to this tenant, staff and members.</summary>
    Tenant = 1,

    /// <summary>The members acting for one organization.</summary>
    Organization = 2,

    /// <summary>The holders of one role, staff or business.</summary>
    Role = 3,

    /// <summary>One user.</summary>
    User = 4,
}

/// <summary>
/// A principal. A <see cref="SharePrincipalKind.Role"/> principal may carry an
/// <see cref="OrganizationId"/>: the role's holders acting for that one organization ("Admin
/// of Acme"), so a member's role reaches only their own organization's items. It sits in the
/// Role tier with plain roles, and a caller matches it when they hold the role and act for the
/// organization (requirement 38).
/// </summary>
public readonly record struct SharePrincipal(SharePrincipalKind Kind, string? Id = null, string? OrganizationId = null)
{
    public static readonly SharePrincipal Anyone = new(SharePrincipalKind.Anyone);

    public static readonly SharePrincipal Tenant = new(SharePrincipalKind.Tenant);

    public static SharePrincipal Organization(string id) => new(SharePrincipalKind.Organization, id);

    public static SharePrincipal Role(string name) => new(SharePrincipalKind.Role, name);

    /// <summary>The holders of <paramref name="name"/> while acting for <paramref name="organizationId"/>.</summary>
    public static SharePrincipal Role(string name, string organizationId) => new(SharePrincipalKind.Role, name, organizationId);

    public static SharePrincipal User(string id) => new(SharePrincipalKind.User, id);

    public override string ToString()
    {
        var kind = Kind.ToString().ToLowerInvariant();
        var text = Id is null ? kind : $"{kind}:{Id}";
        return OrganizationId is null ? text : $"{text}@{OrganizationId}";
    }
}

/// <summary>What a grant allows; each level includes the ones before it.</summary>
public enum AccessLevel
{
    None = 0,

    /// <summary>Read the published version.</summary>
    Viewer = 1,

    /// <summary>Read and comment, nothing more.</summary>
    Commenter = 2,

    /// <summary>Create, read and update; drafts visible.</summary>
    Editor = 3,

    /// <summary>Create, read, update and delete.</summary>
    Manager = 4,

    /// <summary>A Manager who also changes sharing and rules (a drive's Drive Admin). Granted
    /// only on a root object; it covers the whole tree below.</summary>
    Admin = 5,
}

/// <summary>A grant or a block on one object, optionally time-bound (requirements 49-53).</summary>
public sealed record ShareEntry(
    SharePrincipal Principal,
    AccessLevel Level,
    bool IsBlock = false,
    DateTimeOffset? Starts = null,
    DateTimeOffset? Ends = null)
{
    public static ShareEntry Grant(SharePrincipal principal, AccessLevel level, DateTimeOffset? starts = null, DateTimeOffset? ends = null) => new(principal, level, false, starts, ends);

    public static ShareEntry Block(SharePrincipal principal, DateTimeOffset? starts = null, DateTimeOffset? ends = null) => new(principal, AccessLevel.None, true, starts, ends);

    public bool IsActiveAt(DateTimeOffset now) => (Starts is null || Starts <= now) && (Ends is null || now < Ends);
}

public enum ShareConditionOperator
{
    /// <summary>The fact has the value.</summary>
    Equals,

    /// <summary>The fact has one of the values.</summary>
    In,

    /// <summary>The fact has none of the values.</summary>
    NotIn,
}

/// <summary>
/// One condition on a fact a shareable kind reports about its objects (file type, extension,
/// content type, a tag, a metadata value). Facts are multi-valued: <see cref="ShareConditionOperator.Equals"/>
/// and <see cref="ShareConditionOperator.In"/> match when any value matches, compared without case.
/// </summary>
public sealed record ShareCondition(string Fact, ShareConditionOperator Operator, IReadOnlyList<string> Values);

/// <summary>
/// Conditions → grants and blocks (requirements 54-57), defined on an object (applying to the
/// objects below it that match every condition) or on the tenant (applying to every object).
/// Rules on one object apply in their listed order; a later rule overrides an earlier one
/// about the same principal. A rule can be limited to a time window.
/// </summary>
public sealed record ShareRule(
    string Id,
    string Name,
    IReadOnlyList<ShareCondition> Conditions,
    IReadOnlyList<ShareEntry> Effects,
    DateTimeOffset? Starts = null,
    DateTimeOffset? Ends = null)
{
    public bool IsActiveAt(DateTimeOffset now) => (Starts is null || Starts <= now) && (Ends is null || now < Ends);

    public bool Matches(IReadOnlyDictionary<string, IReadOnlyList<string>> facts) => Conditions.All(condition => Matches(condition, facts));

    private static bool Matches(ShareCondition condition, IReadOnlyDictionary<string, IReadOnlyList<string>> facts)
    {
        var values = facts.TryGetValue(condition.Fact, out var found) ? found : [];
        var any = values.Any(value => condition.Values.Contains(value, StringComparer.OrdinalIgnoreCase));
        return condition.Operator switch
        {
            ShareConditionOperator.Equals or ShareConditionOperator.In => any,
            ShareConditionOperator.NotIn => !any,
            _ => false,
        };
    }
}

/// <summary>An object of a shareable kind, by kind name and the module's id for it.</summary>
public readonly record struct ShareableRef(string Kind, string Id)
{
    /// <summary>The tenant itself: where tenant-level entries and rules live.</summary>
    public static readonly ShareableRef TenantRoot = new("tenant", string.Empty);

    public bool IsTenant => Kind == TenantRoot.Kind;

    public override string ToString() => $"{Kind}:{Id}";
}

/// <summary>What a shareable kind tells sharing about one of its objects.</summary>
public sealed class ShareableObject
{
    public required ShareableRef Ref { get; init; }

    /// <summary>The user who owns it: always allowed (requirement 49). Null for objects a
    /// system account owns.</summary>
    public string? OwnerUserId { get; init; }

    /// <summary>The system account that owns it (<c>Grove.Access.SystemAccounts</c>: the tenant's
    /// for a shared drive, an organization's for its drive), when no user does. The tenant or
    /// organization system actor acting as that account is its owner (requirement 33).</summary>
    public string? OwnerAccountId { get; init; }

    /// <summary>The object its grants are inherited from; null for a root.</summary>
    public ShareableRef? Parent { get; init; }

    /// <summary>
    /// Further parents its grants are inherited from too (multi-path inheritance, decision F3 in
    /// docs/sharing.md): a child of a hard-linked folder inherits through every placement of the
    /// folder, not only the one it was created or moved through. Each parent opens one more path
    /// to the root; the caller gets the most a path gives them. Empty for one path.
    /// </summary>
    public IReadOnlyList<ShareableRef> OtherParents { get; init; } = [];

    /// <summary>A top of a tree (a drive): the only place <see cref="AccessLevel.Admin"/> is granted.</summary>
    public bool IsRoot => Parent is null;

    /// <summary>Out of use (an inactive drive, or anything in one): nobody but a tenant
    /// administrator reaches it.</summary>
    public bool IsInactive { get; init; }

    /// <summary>A name for people (a folder's name), shown in the Share panel and the Rules
    /// screens; null when the kind has none to give.</summary>
    public string? DisplayName { get; init; }

    /// <summary>What rules test, by fact name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Facts { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
}
