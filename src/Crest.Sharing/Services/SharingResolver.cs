using Crest.Environment.Extensions.Features;
using Crest.Access;
using Crest.Modules;

namespace Crest.Sharing.Services;

/// <summary>The principals a caller matches, most specific first (docs/sharing.md, requirement 58).</summary>
public static class SharePrincipals
{
    public static IReadOnlyList<SharePrincipal> Of(CallerContext caller)
    {
        var principals = new List<SharePrincipal>();
        if (caller.UserId is not null)
        {
            principals.Add(SharePrincipal.User(caller.UserId));
        }

        principals.AddRange(caller.Roles.Select(SharePrincipal.Role));

        // A member counts for the one organization they act for (requirement 38), and so do
        // their roles scoped to it.
        if (caller.OrganizationId is not null)
        {
            principals.AddRange(caller.Roles.Select(role => SharePrincipal.Role(role, caller.OrganizationId)));
            principals.Add(SharePrincipal.Organization(caller.OrganizationId));
        }

        // Tenant means everyone signed in to this tenant (requirement 37).
        if (caller.IsAuthenticated)
        {
            principals.Add(SharePrincipal.Tenant);
        }

        principals.Add(SharePrincipal.Anyone);
        return principals;
    }

    public static bool Matches(IReadOnlyList<SharePrincipal> principals, SharePrincipal principal) =>
        principals.Any(candidate => candidate.Kind == principal.Kind &&
            string.Equals(candidate.Id, principal.Id, principal.Kind == SharePrincipalKind.Role ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
            string.Equals(candidate.OrganizationId, principal.OrganizationId, StringComparison.Ordinal));
}

/// <summary>A decision with the window it is active in (both bounds from the entry or the rule
/// and its effect; null is open).</summary>
public sealed record TimedDecision(ShareDecision Decision, DateTimeOffset? Starts, DateTimeOffset? Ends)
{
    public bool IsActiveAt(DateTimeOffset now) => (Starts is null || Starts <= now) && (Ends is null || now < Ends);
}

/// <summary>
/// The resolution engine (docs/sharing.md § 4, requirements 49-65). Every entry and every
/// matching rule effect on the object's chain - the tenant, then each object from the root down
/// to the target - is a decision with a principal, a depth and an order. For a person:
/// <list type="number">
/// <item><b>Principal first</b>: the most specific tier (User, Role, Organization, Tenant,
/// Anyone) with any decision about one of the person's principals decides.</item>
/// <item><b>Then depth</b>: within that tier the deepest decision wins.</item>
/// <item><b>Then order, then grant</b>: at that depth the last decision about each principal
/// counts (rules in listed order, then the object's own entries, which override its rules); if
/// the person's principals still disagree the grant wins and grants stack to the highest
/// level.</item>
/// <item><b>Public is absolute for viewing</b>: when the Anyone tier, resolved by depth and
/// order, grants, everyone may view; everything beyond viewing still follows 1-3.</item>
/// </list>
/// No decision is no access (default deny), except the owner: Manager on what they own, Admin
/// when it is a root. Anyone is never more than a Viewer, and Admin counts only on a root.
/// Time bounds are compared with the clock at every resolution, so a share ends on time.
/// Entry sources (<see cref="IShareEntrySource"/>) add their entries and rules at each depth,
/// after the stored ones, and are resolved by exactly the same rules.
/// Nothing is cached beyond the request's documents: a change applies to the next decision.
/// </summary>
public sealed class SharingResolver(
    ShareableKinds kinds,
    IShareStore store,
    IClock clock,
    IEnumerable<IShareEntrySource>? sources = null,
    IProviderPriority? priority = null) : ISharingResolver
{
    // Contributions apply after the stored entries and rules at their depth, lowest priority
    // first, so a higher-priority source about the same principal has the last word.
    private readonly IShareEntrySource[] _sources = [.. (sources ?? [])
        .Select((source, index) => (source, index))
        .OrderBy(entry => priority?.PriorityOf(entry.source.GetType()) ?? 0)
        .ThenBy(entry => entry.index)
        .Select(entry => entry.source)];

    private const int MaxChain = 64;

    public async Task<ShareResolution> ResolveAsync(CallerContext caller, ShareableRef target, CancellationToken cancellationToken = default)
    {
        var chain = await ChainAsync(target, cancellationToken);
        if (chain is null)
        {
            return ShareResolution.Unknown;
        }

        var decisions = await DecisionsAsync(chain);
        var principals = SharePrincipals.Of(caller);

        // Public: the Anyone tier alone, by depth then order.
        var isPublic = Decide(decisions.Where(d => d.Principal.Kind == SharePrincipalKind.Anyone).ToList()) is { IsBlock: false };

        AccessLevel level = AccessLevel.None;
        SharePrincipalKind? decidingTier = null;
        foreach (var tier in new[] { SharePrincipalKind.User, SharePrincipalKind.Role, SharePrincipalKind.Organization, SharePrincipalKind.Tenant, SharePrincipalKind.Anyone })
        {
            var mine = decisions.Where(d => d.Principal.Kind == tier && SharePrincipals.Matches(principals, d.Principal)).ToList();
            if (mine.Count == 0)
            {
                continue;
            }

            decidingTier = tier;
            var winner = Decide(mine);
            level = winner is { IsBlock: false } ? winner.Level : AccessLevel.None;
            break;
        }

        if (isPublic && level < AccessLevel.Viewer)
        {
            level = AccessLevel.Viewer;
        }

        var isOwner = false;
        if (caller.UserId is not null && OwnerLevels(chain).TryGetValue(caller.UserId, out var owned))
        {
            level = Max(level, owned);
            isOwner = true;
        }

        return new ShareResolution(level, isPublic, isOwner, chain.Any(item => item.IsInactive), decidingTier, decisions);
    }

    /// <summary>The object and its parents, root first; null when the kind or an object is unknown or the chain loops.</summary>
    public async Task<IReadOnlyList<ShareableObject>?> ChainAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        var chain = new List<ShareableObject>();
        var seen = new HashSet<ShareableRef>();
        ShareableRef? next = target;
        while (next is { } current)
        {
            if (!seen.Add(current) || chain.Count >= MaxChain || kinds.Find(current.Kind) is not { } kind)
            {
                return null;
            }

            var item = await kind.GetAsync(current.Id, cancellationToken);
            if (item is null)
            {
                return null;
            }

            chain.Add(item);
            next = item.Parent;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// The owners of the target and what owning gives them: the target's owner holds Manager on
    /// it (Admin when it is a root), and the root's owner administers the whole tree (a personal
    /// drive's owner). By user id.
    /// </summary>
    public static IReadOnlyDictionary<string, AccessLevel> OwnerLevels(IReadOnlyList<ShareableObject> chain)
    {
        var owners = new Dictionary<string, AccessLevel>(StringComparer.Ordinal);
        if (chain[^1].OwnerUserId is { } owner)
        {
            owners[owner] = chain[^1].IsRoot ? AccessLevel.Admin : AccessLevel.Manager;
        }

        if (chain[0].OwnerUserId is { } rootOwner)
        {
            owners[rootOwner] = AccessLevel.Admin;
        }

        return owners;
    }

    /// <summary>
    /// Every decision on the chain about the target, from the tenant (depth 0) down: at each
    /// depth the rules defined there that match the target (rules apply below the object they
    /// are defined on; the tenant's apply everywhere) in listed order, then the object's own
    /// entries. Inactive (time-bound) entries and rules are left out.
    /// </summary>
    public async Task<IReadOnlyList<ShareDecision>> DecisionsAsync(IReadOnlyList<ShareableObject> chain)
    {
        var now = new DateTimeOffset(clock.UtcNow, TimeSpan.Zero);
        return [.. (await TimedDecisionsAsync(chain)).Where(decision => decision.IsActiveAt(now)).Select(decision => decision.Decision)];
    }

    /// <summary>
    /// Every decision on the chain, in the order of <see cref="DecisionsAsync"/>, active or not,
    /// each with the window it is active in (a rule effect's is the rule's window intersected
    /// with the effect's). The access index resolves these over time; the resolver over now.
    /// </summary>
    public async Task<IReadOnlyList<TimedDecision>> TimedDecisionsAsync(IReadOnlyList<ShareableObject> chain)
    {
        var target = chain[^1];
        var decisions = new List<TimedDecision>();

        await AddAsync(ShareableRef.TenantRoot, depth: 0, rulesApply: true, isRoot: false, null);
        for (var i = 0; i < chain.Count; i++)
        {
            await AddAsync(chain[i].Ref, depth: i + 1, rulesApply: i < chain.Count - 1, isRoot: chain[i].IsRoot, chain[i]);
        }

        return decisions;

        async Task AddAsync(ShareableRef on, int depth, bool rulesApply, bool isRoot, ShareableObject? item)
        {
            // Admin on a root only for those its kind lets hold it (requirement 19).
            var eligibility = item is not null && isRoot ? kinds.Find(on.Kind) as IShareableAdminEligibility : null;
            ShareEntry Clamp(ShareEntry entry, bool isRoot)
            {
                var clamped = SharingResolver.Clamp(entry, isRoot);
                return clamped.Level == AccessLevel.Admin && !clamped.IsBlock && eligibility is not null && !eligibility.MayHoldAdmin(item!, clamped.Principal)
                    ? clamped with { Level = AccessLevel.Manager }
                    : clamped;
            }

            // The stored sharing first, then each source's contribution: rules, then entries.
            var layers = new List<(string? Source, IReadOnlyList<ShareEntry> Entries, IReadOnlyList<ShareRule> Rules)>();
            if (await store.GetAsync(on) is { } document)
            {
                layers.Add((null, document.Entries, document.Rules));
            }

            foreach (var source in _sources)
            {
                if (await source.GetAsync(on) is { } contribution)
                {
                    layers.Add((source.Name, contribution.Entries, contribution.Rules));
                }
            }

            if (rulesApply)
            {
                foreach (var (sourceName, _, rules) in layers)
                {
                    foreach (var rule in rules.Where(rule => rule.Matches(target.Facts)))
                    {
                        decisions.AddRange(rule.Effects.Select(effect => new TimedDecision(
                            new ShareDecision(on, depth, effect.Principal, Clamp(effect, isRoot), rule.Id, sourceName),
                            Later(rule.Starts, effect.Starts),
                            Earlier(rule.Ends, effect.Ends))));
                    }
                }
            }

            foreach (var (sourceName, entries, _) in layers)
            {
                decisions.AddRange(entries.Select(entry => new TimedDecision(new ShareDecision(on, depth, entry.Principal, Clamp(entry, isRoot), null, sourceName), entry.Starts, entry.Ends)));
            }
        }
    }

    // Anyone is never more than a Viewer (requirement 63); Admin only on a root (requirement 15).
    private static ShareEntry Clamp(ShareEntry entry, bool isRoot)
    {
        if (entry.IsBlock)
        {
            return entry;
        }

        if (entry.Principal.Kind == SharePrincipalKind.Anyone && entry.Level > AccessLevel.Viewer)
        {
            return entry with { Level = AccessLevel.Viewer };
        }

        return entry.Level == AccessLevel.Admin && !isRoot ? entry with { Level = AccessLevel.Manager } : entry;
    }

    private static DateTimeOffset? Later(DateTimeOffset? left, DateTimeOffset? right) =>
        left is null ? right : right is null ? left : left > right ? left : right;

    private static DateTimeOffset? Earlier(DateTimeOffset? left, DateTimeOffset? right) =>
        left is null ? right : right is null ? left : left < right ? left : right;

    /// <summary>
    /// One principal's decision among its own (the access index's per-principal row): the
    /// deepest, and at that depth the last in order. Null for none.
    /// </summary>
    public static ShareDecision? DecideFor(IEnumerable<ShareDecision> decisions)
    {
        ShareDecision? winner = null;
        foreach (var decision in decisions)
        {
            if (winner is null || decision.Depth >= winner.Depth)
            {
                winner = decision;
            }
        }

        return winner;
    }

    /// <summary>Depth, then order per principal, then grant over block with the highest level.</summary>
    private static ShareEntry? Decide(List<ShareDecision> decisions)
    {
        if (decisions.Count == 0)
        {
            return null;
        }

        var deepest = decisions.Max(d => d.Depth);
        var lastPerPrincipal = decisions
            .Where(d => d.Depth == deepest)
            .GroupBy(d => d.Principal)
            .Select(group => group.Last().Effect)
            .ToList();

        var grants = lastPerPrincipal.Where(effect => !effect.IsBlock).ToList();
        return grants.Count > 0 ? grants.MaxBy(effect => effect.Level) : lastPerPrincipal[0];
    }

    private static AccessLevel Max(AccessLevel left, AccessLevel right) => left >= right ? left : right;
}
