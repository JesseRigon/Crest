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

        // A system actor is its system account (requirement 33): it matches the account's owner
        // rows, nothing a person was given.
        if (caller.ActorId is not null)
        {
            principals.Add(SharePrincipal.User(caller.ActorId));
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

    /// <summary>The most paths to the root one object is resolved through (multi-path
    /// inheritance); further ones are left out, which can only give less access.</summary>
    public const int MaxPaths = 16;

    public async Task<ShareResolution> ResolveAsync(CallerContext caller, ShareableRef target, CancellationToken cancellationToken = default)
    {
        var chains = await ChainsAsync(target, cancellationToken);
        if (chains is null)
        {
            return ShareResolution.Unknown;
        }

        // Multi-path inheritance (F3): each path to the root is resolved on its own, and the
        // caller gets the most any path still in use gives them. A path through an inactive
        // drive gives nothing; the object is inactive only when every path is.
        ShareResolution? best = null;
        ShareResolution? inactive = null;
        foreach (var chain in chains)
        {
            var resolution = await ResolveChainAsync(caller, chain);
            if (resolution.IsInactive)
            {
                inactive ??= resolution;
                continue;
            }

            if (best is null)
            {
                best = resolution;
                continue;
            }

            var winner = resolution.Level > best.Level ? resolution : best;
            best = winner with
            {
                IsPublic = best.IsPublic || resolution.IsPublic,
                IsOwner = best.IsOwner || resolution.IsOwner,
            };
        }

        return best ?? inactive!;
    }

    private async Task<ShareResolution> ResolveChainAsync(CallerContext caller, IReadOnlyList<ShareableObject> chain)
    {
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
        if (caller.ActorId is not null && OwnerLevels(chain).TryGetValue(caller.ActorId, out var owned))
        {
            level = Max(level, owned);
            isOwner = true;
        }

        return new ShareResolution(level, isPublic, isOwner, chain.Any(item => item.IsInactive), decidingTier, decisions);
    }

    /// <summary>The object and its parents, root first, along its first parent at every level
    /// (the primary path); null when the kind or an object is unknown or the chain loops.</summary>
    public async Task<IReadOnlyList<ShareableObject>?> ChainAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        var chains = await ChainsAsync(target, cancellationToken);
        return chains?[0];
    }

    /// <summary>
    /// Every path from the root down to the object (multi-path inheritance, F3), root first, the
    /// primary path (first parents all the way) first and at most <see cref="MaxPaths"/>. Null
    /// when the primary path does not resolve; another path that does not resolve (a loop, an
    /// unknown object) is left out.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyList<ShareableObject>>?> ChainsAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        var objects = new Dictionary<ShareableRef, ShareableObject?>();
        async Task<ShareableObject?> GetAsync(ShareableRef reference)
        {
            if (!objects.TryGetValue(reference, out var found))
            {
                found = kinds.Find(reference.Kind) is { } kind ? await kind.GetAsync(reference.Id, cancellationToken) : null;
                objects[reference] = found;
            }

            return found;
        }

        var paths = new List<IReadOnlyList<ShareableObject>>();
        var primaryFailed = false;

        // Depth first, first parents first: the first complete path is the primary one.
        async Task WalkAsync(ShareableRef current, List<ShareableObject> below, HashSet<ShareableRef> seen, bool primary)
        {
            if (paths.Count >= MaxPaths)
            {
                return;
            }

            if (!seen.Add(current) || below.Count >= MaxChain || await GetAsync(current) is not { } item)
            {
                primaryFailed |= primary;
                return;
            }

            var path = new List<ShareableObject>(below) { item };
            if (item.Parent is not { } parent)
            {
                path.Reverse();
                paths.Add(path);
                return;
            }

            await WalkAsync(parent, path, [.. seen], primary);
            foreach (var other in item.OtherParents)
            {
                if (primaryFailed)
                {
                    return;
                }

                await WalkAsync(other, path, [.. seen], primary: false);
            }
        }

        await WalkAsync(target, [], [], primary: true);
        return primaryFailed || paths.Count == 0 ? null : paths;
    }

    /// <summary>
    /// The key of one path for the access index: empty for the primary path (index 0), else a
    /// short hash of the path's objects, so each path's document keeps its key while the paths
    /// above it come and go.
    /// </summary>
    public static string PathKey(IReadOnlyList<ShareableObject> chain, int index)
    {
        if (index == 0)
        {
            return string.Empty;
        }

        var text = string.Join('/', chain.Select(item => item.Ref.ToString()));
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>
    /// The owners of the target and what owning gives them: the target's owner holds Manager on
    /// it (Admin when it is a root), and the root's owner administers the whole tree (a personal
    /// drive's owner). By user id, or system account id for what a system account owns.
    /// </summary>
    public static IReadOnlyDictionary<string, AccessLevel> OwnerLevels(IReadOnlyList<ShareableObject> chain)
    {
        var owners = new Dictionary<string, AccessLevel>(StringComparer.Ordinal);
        if ((chain[^1].OwnerUserId ?? chain[^1].OwnerAccountId) is { } owner)
        {
            owners[owner] = chain[^1].IsRoot ? AccessLevel.Admin : AccessLevel.Manager;
        }

        if ((chain[0].OwnerUserId ?? chain[0].OwnerAccountId) is { } rootOwner)
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
