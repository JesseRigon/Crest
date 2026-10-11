using Crest.Modules;
using Microsoft.Extensions.Logging;

namespace Crest.Sharing.Services;

/// <summary>
/// Keeps the access index (docs/sharing.md, requirements 75-82; decision F3, option chosen:
/// one document per object holding each principal's winning decision over time).
/// <list type="bullet">
/// <item><b>Calculation</b> uses the resolver's own chain and decisions
/// (<see cref="SharingResolver.TimedDecisionsAsync"/>), so the index and a single decision come
/// from the same entries, rules and contributions (81). For each window between the decisions'
/// start and end times, each principal's winner (deepest, then last) is a row with that window;
/// owners get an owner row; an inactive object gets none.</item>
/// <item><b>Recalculation</b> covers the changed object and everything below it (78), through
/// the kinds' <see cref="IShareableTree"/>. A change that may remove access is applied before
/// the call returns, in batches (80); a change that only adds access is applied at once up to
/// <see cref="SynchronousLimit"/> objects and otherwise queued for the job.</item>
/// <item><b>Time</b>: rows carry their windows, so access starts and ends on time at every read;
/// each document registers its next boundary, and the job recalculates the ones due (79),
/// which drops windows that have passed.</item>
/// </list>
/// </summary>
public sealed class ShareAccessIndexer(
    ShareableKinds kinds,
    SharingResolver resolver,
    IShareAccessStore store,
    IClock clock,
    ILogger<ShareAccessIndexer>? logger = null) : IShareAccessIndex
{
    /// <summary>The most objects a change that only adds access recalculates before returning;
    /// a larger subtree is queued.</summary>
    public const int SynchronousLimit = 200;

    /// <summary>The objects written per flush during a long recalculation.</summary>
    public const int BatchSize = 100;

    private const int MaxDepth = 64;

    private DateTime Now => DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);

    public Task RecalculateAsync(ShareableRef target, bool includeDescendants = false, CancellationToken cancellationToken = default) =>
        includeDescendants ? ApplyAsync(target, narrows: true, cancellationToken) : RecalculateOneAsync(target, cancellationToken);

    /// <summary>The queue entry that stands for a rebuild of the whole index.</summary>
    public static readonly ShareableRef Rebuild = new("rebuild", string.Empty);

    public Task QueueRebuildAsync(CancellationToken cancellationToken = default) => store.QueueAsync(Rebuild, Now);

    public async Task RemoveAsync(ShareableRef target, bool includeDescendants = false, CancellationToken cancellationToken = default)
    {
        var targets = includeDescendants ? await SubtreeAsync(target, int.MaxValue, cancellationToken) : [target];
        foreach (var item in targets!)
        {
            await store.DeleteAsync(item);
        }
    }

    /// <summary>
    /// Applies a change to the sharing on <paramref name="target"/> (the tenant for a tenant-level
    /// change). <paramref name="narrows"/>: the change may remove access, so every object it
    /// reaches is recalculated before this returns; otherwise a large reach is queued.
    /// </summary>
    public async Task ApplyAsync(ShareableRef target, bool narrows, CancellationToken cancellationToken = default)
    {
        if (target.IsTenant)
        {
            if (!narrows)
            {
                await store.QueueAsync(target, Now);
                return;
            }

            await RecalculateEverythingAsync(cancellationToken);
            return;
        }

        var limit = narrows ? int.MaxValue : SynchronousLimit;
        var subtree = await SubtreeAsync(target, limit, cancellationToken);
        if (subtree is null)
        {
            await store.QueueAsync(target, Now);
            return;
        }

        await RecalculateAllAsync(subtree, cancellationToken);
    }

    /// <summary>Recalculates the objects whose next boundary has come (the job), returning how many.</summary>
    public async Task<int> RecalculateDueAsync(int max, CancellationToken cancellationToken = default)
    {
        var due = await store.DueAsync(Now, max);
        await RecalculateAllAsync(due, cancellationToken);
        return due.Count;
    }

    /// <summary>Runs the queued background recalculations (the job), returning how many.</summary>
    public async Task<int> RecalculateQueuedAsync(int max, CancellationToken cancellationToken = default)
    {
        var queued = await store.QueuedAsync(max);
        foreach (var recalculation in queued)
        {
            if (recalculation.Ref == Rebuild)
            {
                // A rebuild becomes one queued subtree per root, run a few per pass.
                foreach (var kind in kinds.All.OfType<IShareableRoots>())
                {
                    foreach (var root in await kind.RootsAsync(cancellationToken))
                    {
                        await store.QueueAsync(root, Now);
                    }
                }
            }
            else if (recalculation.Ref.IsTenant)
            {
                await RecalculateEverythingAsync(cancellationToken);
            }
            else
            {
                await RecalculateAllAsync(await SubtreeAsync(recalculation.Ref, int.MaxValue, cancellationToken) ?? [], cancellationToken);
            }

            await store.DequeueAsync(recalculation);
        }

        return queued.Count;
    }

    /// <summary>
    /// Verifies one object (requirement 81): recalculates it and compares the result with its
    /// stored document from now on. Empty when they agree; otherwise each difference.
    /// </summary>
    public async Task<IReadOnlyList<string>> VerifyAsync(ShareableRef target, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var expected = await CalculateAsync(target, cancellationToken);
        var stored = await store.GetAsync(target);
        var differences = new List<string>();

        if (expected is null || stored is null)
        {
            if (expected is not null || stored is not null)
            {
                differences.Add(expected is null ? $"{target}: indexed, but it no longer resolves." : $"{target}: not indexed.");
            }

            return differences;
        }

        if (expected.IsInactive != stored.IsInactive)
        {
            differences.Add($"{target}: inactive is {stored.IsInactive}, expected {expected.IsInactive}.");
        }

        var want = Signatures(expected.Rows, now);
        var have = Signatures(stored.Rows, now);
        differences.AddRange(want.Except(have).Select(row => $"{target}: missing {row}."));
        differences.AddRange(have.Except(want).Select(row => $"{target}: unexpected {row}."));

        var wantShadows = ShadowSignatures(expected.Shadows, now);
        var haveShadows = ShadowSignatures(stored.Shadows, now);
        differences.AddRange(wantShadows.Except(haveShadows).Select(row => $"{target}: missing shadow {row}."));
        differences.AddRange(haveShadows.Except(wantShadows).Select(row => $"{target}: unexpected shadow {row}."));
        return differences;
    }

    /// <summary>
    /// The object's access as it would be indexed now; null when it does not resolve (an unknown
    /// kind or object, a broken chain), which leaves it out of every list.
    /// </summary>
    public async Task<ShareAccessDocument?> CalculateAsync(ShareableRef target, CancellationToken cancellationToken = default)
    {
        var chain = await resolver.ChainAsync(target, cancellationToken);
        if (chain is null)
        {
            return null;
        }

        var now = Now;
        var document = new ShareAccessDocument { Kind = target.Kind, ObjectId = target.Id, CalculatedUtc = now };
        if (chain.Any(item => item.IsInactive))
        {
            document.IsInactive = true;
            return document;
        }

        var decisions = await resolver.TimedDecisionsAsync(chain);
        var boundaries = decisions
            .SelectMany(decision => new[] { decision.Starts, decision.Ends })
            .OfType<DateTimeOffset>()
            .Select(boundary => boundary.UtcDateTime)
            .Where(boundary => boundary > now)
            .Distinct()
            .Order()
            .ToList();

        // Windows: [.., b1), [b1, b2), ..., [bn, ..). The first is open at the start: the past
        // is never read.
        var open = new Dictionary<SharePrincipal, ShareAccessRow>();
        for (var i = 0; i <= boundaries.Count; i++)
        {
            DateTime? starts = i == 0 ? null : boundaries[i - 1];
            DateTime? ends = i == boundaries.Count ? null : boundaries[i];
            var at = new DateTimeOffset(starts ?? now, TimeSpan.Zero);

            var winners = decisions
                .Where(decision => decision.IsActiveAt(at))
                .GroupBy(decision => decision.Decision.Principal)
                .Select(group => SharingResolver.DecideFor(group.Select(decision => decision.Decision))!)
                .ToList();

            foreach (var principal in open.Keys.Except(winners.Select(winner => winner.Principal)).ToList())
            {
                open.Remove(principal);
            }

            foreach (var winner in winners)
            {
                var effect = winner.Effect;
                var level = effect.IsBlock ? AccessLevel.None : effect.Level;
                if (open.TryGetValue(winner.Principal, out var current)
                    && current.Depth == winner.Depth && current.Level == level && current.IsBlock == effect.IsBlock
                    && current.RuleId == winner.RuleId && current.Source == winner.Source && current.EndsUtc == starts)
                {
                    // The same winner goes on into the next window.
                    current.EndsUtc = ends;
                    continue;
                }

                var row = new ShareAccessRow
                {
                    PrincipalKind = winner.Principal.Kind,
                    PrincipalId = winner.Principal.Id ?? string.Empty,
                    OrganizationId = winner.Principal.OrganizationId,
                    Depth = winner.Depth,
                    Level = level,
                    IsBlock = effect.IsBlock,
                    StartsUtc = starts,
                    EndsUtc = ends,
                    RuleId = winner.RuleId,
                    Source = winner.Source,
                    IsDirect = winner.Depth == chain.Count && winner.RuleId is null,
                };
                document.Rows.Add(row);
                open[winner.Principal] = row;
            }
        }

        // Owners hold their level whatever the user tier says (requirement 49), at all times.
        foreach (var (owner, level) in SharingResolver.OwnerLevels(chain))
        {
            document.Rows.RemoveAll(row => row.PrincipalKind == SharePrincipalKind.User && string.Equals(row.PrincipalId, owner, StringComparison.Ordinal));
            document.Rows.Add(new ShareAccessRow
            {
                PrincipalKind = SharePrincipalKind.User,
                PrincipalId = owner,
                Depth = chain.Count,
                Level = level,
                IsOwner = true,
            });
        }

        // Within the role tier the deepest row wins and, at one depth, the highest grant. A role
        // row outranks role grants shallower than it and of a higher level: for a threshold above
        // its level they no longer count (a block cancels them all; a Viewer row deeper than an
        // Editor grant leaves Viewer). At one depth nothing is cancelled: the grants stack.
        var roleRows = document.Rows.Where(row => row.PrincipalKind == SharePrincipalKind.Role).ToList();
        foreach (var deeper in roleRows)
        {
            var deeperLevel = deeper.Admits ? deeper.Level : AccessLevel.None;
            foreach (var depth in roleRows.Where(row => row.Admits && row.Depth < deeper.Depth && row.Level > deeperLevel).Select(row => row.Depth).Distinct())
            {
                document.Shadows.Add(new ShareRoleShadow
                {
                    Role = deeper.PrincipalId,
                    OrganizationId = deeper.OrganizationId,
                    Depth = depth,
                    Level = deeperLevel,
                    StartsUtc = deeper.StartsUtc,
                    EndsUtc = deeper.EndsUtc,
                });
            }
        }

        document.IsPublic = document.Rows.Any(row => row.PrincipalKind == SharePrincipalKind.Anyone && row.Admits && row.IsActiveAt(now));
        document.NextBoundaryUtc = boundaries.Count > 0 ? boundaries[0] : null;
        return document;
    }

    private async Task RecalculateOneAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        var document = await CalculateAsync(target, cancellationToken);
        if (document is null)
        {
            await store.DeleteAsync(target);
            return;
        }

        await store.SaveAsync(document);
    }

    private async Task RecalculateAllAsync(IReadOnlyList<ShareableRef> targets, CancellationToken cancellationToken)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RecalculateOneAsync(targets[i], cancellationToken);
            if ((i + 1) % BatchSize == 0)
            {
                await store.FlushAsync();
            }
        }
    }

    /// <summary>A tenant-level change reaches every object: every calculated one is recalculated,
    /// a page at a time.</summary>
    private async Task RecalculateEverythingAsync(CancellationToken cancellationToken)
    {
        long cursor = 0;
        while (true)
        {
            var (page, next) = await store.PageAsync(cursor, BatchSize);
            if (page.Count == 0)
            {
                return;
            }

            await RecalculateAllAsync(page, cancellationToken);
            await store.FlushAsync();
            cursor = next;
        }
    }

    /// <summary>
    /// The object and everything below it, parents before children; null when there are more
    /// than <paramref name="limit"/>. A kind that is not an <see cref="IShareableTree"/> has no
    /// children.
    /// </summary>
    public async Task<IReadOnlyList<ShareableRef>?> SubtreeAsync(ShareableRef target, int limit, CancellationToken cancellationToken = default)
    {
        var found = new List<ShareableRef> { target };
        var seen = new HashSet<ShareableRef> { target };
        var level = new List<ShareableRef> { target };
        for (var depth = 0; depth < MaxDepth && level.Count > 0; depth++)
        {
            var next = new List<ShareableRef>();
            foreach (var parent in level)
            {
                if (kinds.Find(parent.Kind) is not IShareableTree tree)
                {
                    continue;
                }

                foreach (var child in await tree.ChildrenAsync(parent.Id, cancellationToken))
                {
                    if (!seen.Add(child))
                    {
                        continue;
                    }

                    if (found.Count >= limit)
                    {
                        return null;
                    }

                    found.Add(child);
                    next.Add(child);
                }
            }

            level = next;
        }

        if (level.Count > 0)
        {
            logger?.LogWarning("The tree below {Target} is deeper than {MaxDepth}; objects below that depth were not recalculated.", target, MaxDepth);
        }

        return found;
    }

    private static HashSet<string> Signatures(IEnumerable<ShareAccessRow> rows, DateTime now) =>
        [.. rows
            .Where(row => row.EndsUtc is null || row.EndsUtc > now)
            .Select(row => $"{row.Principal} depth {row.Depth} {(row.IsBlock ? "block" : row.Level.ToString())}{(row.IsOwner ? " owner" : string.Empty)} [{(row.StartsUtc is { } s && s > now ? s.ToString("O") : "..")}, {row.EndsUtc?.ToString("O") ?? ".."})")];

    private static HashSet<string> ShadowSignatures(IEnumerable<ShareRoleShadow> shadows, DateTime now) =>
        [.. shadows
            .Where(shadow => shadow.EndsUtc is null || shadow.EndsUtc > now)
            .Select(shadow => $"{shadow.Role}@{shadow.OrganizationId} at {shadow.Level} over depth {shadow.Depth} [{(shadow.StartsUtc is { } s && s > now ? s.ToString("O") : "..")}, {shadow.EndsUtc?.ToString("O") ?? ".."})")];
}
