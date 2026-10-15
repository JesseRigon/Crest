using Grove.Access;
using Grove.Modules;
using Grove.Access.Sharing.Indexes;

namespace Grove.Access.Sharing.Services;

/// <summary>
/// Sharing's list and search scopes (requirement 77): one <see cref="IScopeProvider"/> per
/// shareable kind that declares where its objects are rows (<see cref="IShareableScopeTarget"/>).
/// Kinds without one are decided object by object only, by the mapper.
/// </summary>
public sealed class SharingScopeProviderSource(ShareableKinds kinds, IClock clock) : IScopeProviderSource
{
    public IEnumerable<IScopeProvider> GetProviders() =>
        kinds.All
            .OfType<IShareableScopeTarget>()
            .Select(kind => new SharingScopeProvider(((IShareableKind)kind).Name, kind.ScopeTarget, clock));
}

/// <summary>Sharing's list filter for a kind, for modules that combine it with their own scope.</summary>
public sealed class SharingScopes(ShareableKinds kinds, IClock clock) : IShareScopes
{
    public ScopeRule? RuleFor(CallerContext caller, string kind, AccessLevel minimum) =>
        kinds.Find(kind) is IShareableScopeTarget target
            ? new SharingScopeProvider(kind, target.ScopeTarget, clock).Build(caller, minimum)
            : null;

    public ScopeRule? IndicatorRuleFor(string kind, ShareIndicator indicator) =>
        kinds.Find(kind) is IShareableScopeTarget target
            ? SharingScopeProvider.Indicator(kind, target.ScopeTarget, indicator)
            : null;
}

/// <summary>
/// The rows of a kind's index table a caller may view, as semi-joins over the access index
/// (docs/sharing.md, requirements 58-62, 75-77). An object is visible when:
/// <list type="number">
/// <item>it is public (an Anyone row grants now); or</item>
/// <item>the caller's user row grants; or, with no user row,</item>
/// <item>one of the caller's role rows (plain, or scoped to the organization they act for)
/// grants and no deeper role row of theirs below the level outranks it (a
/// <see cref="ShareRoleShadowIndex"/> row); or, with no role row,</item>
/// <item>their organization's row grants; or, with no organization row,</item>
/// <item>the tenant row grants (signed-in callers only).</item>
/// </list>
/// Only rows active now count, so an ended share is out of every list at once. The tiers are
/// read per path to the root (one access document per path, multi-path inheritance F3), and an
/// object is visible when any path admits the caller. The tenant
/// administrator sees every row. This is the resolver's tier logic restated over the index rows,
/// and the tests hold the two to each other (requirement 81).
/// </summary>
public sealed class SharingScopeProvider(string kind, ShareScopeTarget target, IClock clock) : IScopeProvider
{
    private const string Access = nameof(ShareAccessIndex);
    private const string Shadows = nameof(ShareRoleShadowIndex);
    private const string Paths = nameof(ShareAccessObjectIndex);

    public string Kind => kind;

    public string Target => target.Table;

    public int Version => 2;

    public Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default) =>
        Task.FromResult(Build(caller));

    /// <summary>The objects the caller may view.</summary>
    public ScopeRule Build(CallerContext caller) => Build(caller, AccessLevel.Viewer);

    /// <summary>
    /// The objects on which the caller's level is at least <paramref name="minimum"/> (Viewer
    /// for lists; Editor for "may see drafts"). Public counts only for Viewer; owners always.
    /// </summary>
    public ScopeRule Build(CallerContext caller, AccessLevel minimum)
    {
        if (caller.IsSuperUser)
        {
            return ScopeRule.All;
        }

        var now = DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);
        var principals = SharePrincipals.Of(caller);
        string[] Ids(SharePrincipalKind tier) =>
            [.. principals.Where(principal => principal.Kind == tier).Select(ShareAccessIndexProvider.IndexedId).Distinct(StringComparer.Ordinal)];

        // From the least specific tier up: each tier decides when it has a row, else defers.
        ScopeFilter? visible = null;
        foreach (var tier in new[] { SharePrincipalKind.Tenant, SharePrincipalKind.Organization, SharePrincipalKind.Role, SharePrincipalKind.User })
        {
            var ids = Ids(tier);
            if (ids.Length == 0)
            {
                continue;
            }

            var grants = tier == SharePrincipalKind.Role ? RoleGrants(ids, now, minimum) : In(Rows(tier, ids, now, minimum));
            visible = visible is null
                ? grants
                : ScopeFilter.AnyOf(grants, ScopeFilter.AllOf(NotIn(Rows(tier, ids, now, minimum: null)), visible));
        }

        // Public is absolute for viewing (requirement 62), and never more than viewing.
        if (minimum > AccessLevel.Viewer)
        {
            return visible is null ? ScopeRule.None : ScopeRule.Where(Objects(visible));
        }

        var isPublic = In(Rows(SharePrincipalKind.Anyone, [string.Empty], now, minimum));
        return ScopeRule.Where(Objects(visible is null ? isPublic : ScopeFilter.AnyOf(isPublic, visible)));
    }

    /// <summary>
    /// The rows whose object has <paramref name="indicator"/> on any path (requirement 98): public,
    /// shared (and not public), or neither. An object never calculated is private.
    /// </summary>
    public static ScopeRule Indicator(string kind, ShareScopeTarget target, ShareIndicator indicator)
    {
        ScopeFilter Having(string column) =>
            ScopeFilter.Of(ScopeCondition.InSelect(target.IdColumn, Paths, nameof(ShareAccessObjectIndex.ObjectId), ScopeFilter.AllOf(
                ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareAccessObjectIndex.Kind), kind)),
                ScopeFilter.Of(ScopeCondition.Equal(column, true)))));

        ScopeFilter NotHaving(string column) =>
            ScopeFilter.Of(ScopeCondition.NotInSelect(target.IdColumn, Paths, nameof(ShareAccessObjectIndex.ObjectId), ScopeFilter.AllOf(
                ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareAccessObjectIndex.Kind), kind)),
                ScopeFilter.Of(ScopeCondition.Equal(column, true)))));

        return indicator switch
        {
            ShareIndicator.Public => ScopeRule.Where(Having(nameof(ShareAccessObjectIndex.IsPublic))),
            ShareIndicator.Shared => ScopeRule.Where(ScopeFilter.AllOf(Having(nameof(ShareAccessObjectIndex.IsShared)), NotHaving(nameof(ShareAccessObjectIndex.IsPublic)))),
            _ => ScopeRule.Where(ScopeFilter.AllOf(NotHaving(nameof(ShareAccessObjectIndex.IsShared)), NotHaving(nameof(ShareAccessObjectIndex.IsPublic)))),
        };
    }

    /// <summary>
    /// The tier logic above runs per path to the root (multi-path inheritance, F3): over the
    /// per-path registry, correlated by access key. An object is in the list when any of its
    /// paths admits the caller.
    /// </summary>
    private ScopeFilter Objects(ScopeFilter paths) =>
        ScopeFilter.Of(ScopeCondition.InSelect(target.IdColumn, Paths, nameof(ShareAccessObjectIndex.ObjectId), ScopeFilter.AllOf(
            ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareAccessObjectIndex.Kind), kind)),
            paths)));

    /// <summary>A role grant of at least <paramref name="minimum"/> that no deeper row of the
    /// caller's roles below <paramref name="minimum"/> outranks.</summary>
    private ScopeFilter RoleGrants(string[] roles, DateTime now, AccessLevel minimum) =>
        In(ScopeFilter.AllOf(
            Rows(SharePrincipalKind.Role, roles, now, minimum),
            ScopeFilter.Of(ScopeCondition.NotInSelect(
                nameof(ShareAccessIndex.DepthKey),
                Shadows,
                nameof(ShareRoleShadowIndex.DepthKey),
                ScopeFilter.AllOf(
                    ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareRoleShadowIndex.Kind), kind)),
                    ScopeFilter.Of(ScopeCondition.In(nameof(ShareRoleShadowIndex.Role), roles)),
                    ScopeFilter.Of(ScopeCondition.LessThan(nameof(ShareRoleShadowIndex.Level), (int)minimum)),
                    Active(nameof(ShareRoleShadowIndex.StartsUtc), nameof(ShareRoleShadowIndex.EndsUtc), now))))));

    /// <summary>This kind's access rows about the principals, active now; only those granting
    /// at least <paramref name="minimum"/> when it is set.</summary>
    private ScopeFilter Rows(SharePrincipalKind tier, string[] ids, DateTime now, AccessLevel? minimum)
    {
        var conditions = new List<ScopeFilter>
        {
            ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareAccessIndex.Kind), kind)),
            ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareAccessIndex.PrincipalKind), (int)tier)),
            ids.Length == 1
                ? ScopeFilter.Of(ScopeCondition.Equal(nameof(ShareAccessIndex.PrincipalId), ids[0]))
                : ScopeFilter.Of(ScopeCondition.In(nameof(ShareAccessIndex.PrincipalId), ids)),
            Active(nameof(ShareAccessIndex.StartsUtc), nameof(ShareAccessIndex.EndsUtc), now),
        };

        if (minimum is { } level)
        {
            conditions.Add(ScopeFilter.Of(ScopeCondition.GreaterThanOrEqual(nameof(ShareAccessIndex.Level), (int)(level < AccessLevel.Viewer ? AccessLevel.Viewer : level))));
        }

        return ScopeFilter.AllOf([.. conditions]);
    }

    /// <summary>Started (or open) and not yet ended (or open).</summary>
    private static ScopeFilter Active(string starts, string ends, DateTime now) =>
        ScopeFilter.AllOf(
            ScopeFilter.AnyOf(ScopeFilter.Of(ScopeCondition.IsNull(starts)), ScopeFilter.Of(ScopeCondition.LessThanOrEqual(starts, now))),
            ScopeFilter.AnyOf(ScopeFilter.Of(ScopeCondition.IsNull(ends)), ScopeFilter.Of(ScopeCondition.GreaterThan(ends, now))));

    private static ScopeFilter In(ScopeFilter rows) =>
        ScopeFilter.Of(ScopeCondition.InSelect(nameof(ShareAccessObjectIndex.AccessKey), Access, nameof(ShareAccessIndex.AccessKey), rows));

    private static ScopeFilter NotIn(ScopeFilter rows) =>
        ScopeFilter.Of(ScopeCondition.NotInSelect(nameof(ShareAccessObjectIndex.AccessKey), Access, nameof(ShareAccessIndex.AccessKey), rows));
}
