using Grove.Access;

namespace Grove.Access.Sharing;

/// <summary>
/// Registered by a module to make its items shareable (docs/sharing.md › Ruling): how to
/// recognise one of its resources, what sharing needs to know about an object, and which of
/// the module's permissions take which access level. Register it only while the sharing
/// feature is on (<c>[RequireFeatures("Grove.Access.Sharing")]</c>); for a registered kind the
/// sharing decision replaces the module's own.
/// </summary>
public interface IShareableKind
{
    /// <summary>The kind's name, stable: it is stored with every entry ("file", "content").</summary>
    string Name { get; }

    /// <summary>The object id when <paramref name="resource"/> is one of this kind's (the
    /// resource a permission is asked against).</summary>
    bool TryIdentify(object resource, out string id);

    /// <summary>The object, or null when it does not exist.</summary>
    Task<ShareableObject?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>The access level <paramref name="permission"/> takes on this kind's objects;
    /// null when the permission is not about them (the module's own mapper then decides).</summary>
    AccessLevel? LevelFor(string permission);
}

/// <summary>How a caller's level on an object was reached, for the explanation (requirement 66).
/// <see cref="Source"/> names the <see cref="IShareEntrySource"/> that contributed it; null for
/// the entries and rules stored by sharing itself.</summary>
public sealed record ShareDecision(
    ShareableRef DefinedOn,
    int Depth,
    SharePrincipal Principal,
    ShareEntry Effect,
    string? RuleId,
    string? Source = null);

/// <summary>Entries and rules a source contributes for one object (or the tenant).</summary>
public sealed record ShareContribution(IReadOnlyList<ShareEntry> Entries, IReadOnlyList<ShareRule> Rules)
{
    public static readonly ShareContribution None = new([], []);
}

/// <summary>
/// How a module <b>adds</b> sharing to items without becoming their authority
/// (docs/sharing.md › Extending or replacing sharing): it contributes entries and rules for an
/// object - grants from an external system, blocks from a business relationship - and the
/// sharing engine resolves them with the stored ones under the same precedence (principal tier,
/// depth, order, blocks, Anyone only Viewer, Admin only on a root). A contribution therefore
/// wins only where sharing's own rules let it; it cannot grant past a deeper block or a more
/// specific principal's decision. At one depth contributions come after the stored entries and
/// rules, in feature-priority order, so a later (higher-priority) contribution about the same
/// principal overrides an earlier one. To replace sharing for some items instead, ship a
/// higher-priority authoritative mapper; to narrow, a ceiling.
/// </summary>
public interface IShareEntrySource
{
    /// <summary>A short name for the explanation ("parties", "crm").</summary>
    string Name { get; }

    /// <summary>The source's entries and rules on <paramref name="target"/>; null for none.</summary>
    Task<ShareContribution?> GetAsync(ShareableRef target, CancellationToken cancellationToken = default);
}

public sealed record ShareResolution(
    AccessLevel Level,
    bool IsPublic,
    bool IsOwner,
    bool IsInactive,
    SharePrincipalKind? DecidingTier,
    IReadOnlyList<ShareDecision> Decisions)
{
    public static readonly ShareResolution Unknown = new(AccessLevel.None, false, false, false, null, []);

    public bool Allows(AccessLevel level) => !IsInactive && Level >= level;
}

/// <summary>The effective access level of a caller on an object (requirements 58-65).</summary>
public interface ISharingResolver
{
    Task<ShareResolution> ResolveAsync(CallerContext caller, ShareableRef target, CancellationToken cancellationToken = default);
}

/// <summary>
/// Changes and reads sharing, as the gated caller. Changing an object's sharing takes Admin on
/// its tree (a drive's Drive Admin) or a tenant administrator; tenant-level entries and rules
/// take <c>ManageTenantSharing</c>; granting Anyone takes <c>ShareWithAnyone</c> (requirement
/// 85). Every change is audited (99). Refusals throw <see cref="AccessDeniedException"/>.
/// </summary>
public interface ISharingService
{
    Task<IReadOnlyList<ShareEntry>> GetEntriesAsync(ShareableRef target, CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces the entry for the entry's principal on the object.</summary>
    Task SetEntryAsync(ShareableRef target, ShareEntry entry, CancellationToken cancellationToken = default);

    Task RemoveEntryAsync(ShareableRef target, SharePrincipal principal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShareRule>> GetRulesAsync(ShareableRef target, CancellationToken cancellationToken = default);

    /// <summary>Replaces the object's rules, in run order.</summary>
    Task SetRulesAsync(ShareableRef target, IReadOnlyList<ShareRule> rules, CancellationToken cancellationToken = default);

    /// <summary>Every entry, block and matching rule effect on the object's chain, from the
    /// tenant down, in order; only for the tree's Admins and tenant administrators
    /// (requirement 66).</summary>
    Task<IReadOnlyList<ShareDecision>> ExplainAsync(ShareableRef target, CancellationToken cancellationToken = default);

    /// <summary>The objects shared with the caller, their roles, organization or the tenant by an
    /// explicit grant that resolves to at least Viewer for them.</summary>
    Task<IReadOnlyList<ShareableRef>> SharedWithMeAsync(CancellationToken cancellationToken = default);
}

public static class SharingPermissionNames
{
    /// <summary>Tenant-level entries and rules, which reach every object.</summary>
    public const string ManageTenantSharing = "ManageTenantSharing";

    /// <summary>Granting Anyone: making something public.</summary>
    public const string ShareWithAnyone = "ShareWithAnyone";
}
