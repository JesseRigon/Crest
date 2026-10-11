using Crest.Access;

namespace Crest.Sharing.Services;

/// <summary>
/// Reads and changes sharing as the gated caller (docs/sharing.md, requirements 66, 85, 99).
/// An object's sharing is seen and changed by its tree's Admins (a drive's Drive Admins) and
/// tenant administrators only; tenant-level entries and rules take
/// <see cref="SharingPermissionNames.ManageTenantSharing"/>; a grant to Anyone takes
/// <see cref="SharingPermissionNames.ShareWithAnyone"/>. Every change is recorded. A change
/// applies to the next decision: nothing is cached across requests. Each change also updates
/// the access index for the object and everything below it (requirement 78); one that may
/// remove access is applied before the call returns (80).
/// </summary>
public sealed class SharingService(
    ICallerContextAccessor callers,
    IAccessDecision decision,
    ISharingResolver resolver,
    IShareStore store,
    ShareableKinds kinds,
    IAccessAuditor auditor,
    ShareAccessIndexer? index = null,
    IShareAccessStore? accessStore = null,
    Crest.Modules.IClock? clock = null) : ISharingService
{
    public async Task<IReadOnlyList<ShareEntry>> GetEntriesAsync(ShareableRef target, CancellationToken cancellationToken = default)
    {
        // A holder of the kind's admin-assignment permission sees the Admin grants they manage,
        // nothing else of the root's sharing.
        if (await AdminAssignerAsync(target, cancellationToken) is { } && !await IsAdminAsync(target, cancellationToken))
        {
            return [.. ((await store.GetAsync(target))?.Entries ?? []).Where(IsAdminGrant)];
        }

        await RequireAdminAsync(target, cancellationToken);
        return (await store.GetAsync(target))?.Entries ?? [];
    }

    public async Task SetEntryAsync(ShareableRef target, ShareEntry entry, CancellationToken cancellationToken = default)
    {
        // Admin on a root of a kind whose admins the tenant assigns (media requirement 31): the
        // tenant permission suffices; anything else takes the tree's Admin.
        var through = IsAdminGrant(entry) ? await AdminAssignerAsync(target, cancellationToken) : null;
        var caller = through is not null && !await IsAdminAsync(target, cancellationToken) ? callers.Current! : await RequireAdminAsync(target, cancellationToken);
        await ValidateAsync(caller, target, [entry], cancellationToken);

        var document = await DocumentAsync(target);
        var previous = document.Entries.LastOrDefault(existing => existing.Principal == entry.Principal);
        document.Entries.RemoveAll(existing => existing.Principal == entry.Principal);
        document.Entries.Add(entry);
        await store.SaveAsync(document);
        await IndexAsync(target, narrows: !Widens(previous, entry), cancellationToken);
        await RecordAsync(caller, "Sharing.SetEntry", target, Detail(entry.ToString(), through), cancellationToken);
    }

    public async Task RemoveEntryAsync(ShareableRef target, SharePrincipal principal, CancellationToken cancellationToken = default)
    {
        var existing = (await store.GetAsync(target))?.Entries.Where(entry => entry.Principal == principal).ToList() ?? [];
        var through = existing.Count > 0 && existing.All(IsAdminGrant) ? await AdminAssignerAsync(target, cancellationToken) : null;
        var caller = through is not null && !await IsAdminAsync(target, cancellationToken) ? callers.Current! : await RequireAdminAsync(target, cancellationToken);
        var document = await store.GetAsync(target);
        var removed = document?.Entries.Where(existing => existing.Principal == principal).ToList() ?? [];
        if (document is null || document.Entries.RemoveAll(existing => existing.Principal == principal) == 0)
        {
            return;
        }

        await store.SaveAsync(document);

        // Removing a block only adds access; removing a grant may take it away.
        await IndexAsync(target, narrows: removed.Any(entry => !entry.IsBlock), cancellationToken);
        await RecordAsync(caller, "Sharing.RemoveEntry", target, Detail(principal.ToString(), through), cancellationToken);
    }

    public async Task<IReadOnlyList<ShareRule>> GetRulesAsync(ShareableRef target, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(target, cancellationToken);
        return (await store.GetAsync(target))?.Rules ?? [];
    }

    public async Task SetRulesAsync(ShareableRef target, IReadOnlyList<ShareRule> rules, CancellationToken cancellationToken = default)
    {
        var caller = await RequireAdminAsync(target, cancellationToken);

        // A rule applies below the object it is defined on, never to the object itself, so a
        // rule effect is never an Admin grant on a root; Admin in an effect is refused.
        await ValidateAsync(caller, target, [.. rules.SelectMany(rule => rule.Effects)], cancellationToken, effectsBelow: true);
        if (rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count() != rules.Count)
        {
            throw new ArgumentException("Rule ids must be unique on one object.", nameof(rules));
        }

        var document = await DocumentAsync(target);
        document.Rules = [.. rules];
        await store.SaveAsync(document);

        // Rules can narrow in ways a comparison would not see (an order change, a condition):
        // always applied before returning.
        await IndexAsync(target, narrows: true, cancellationToken);
        await RecordAsync(caller, "Sharing.SetRules", target, $"{rules.Count} rule(s)", cancellationToken);
    }

    public async Task<IReadOnlyList<ShareDecision>> ExplainAsync(ShareableRef target, CancellationToken cancellationToken = default)
    {
        var caller = await RequireAdminAsync(target, cancellationToken);
        return (await resolver.ResolveAsync(caller, target, cancellationToken)).Decisions;
    }

    public async Task<IReadOnlyList<ShareableRef>> SharedWithMeAsync(CancellationToken cancellationToken = default)
    {
        if (callers.Current is not { IsAuthenticated: true } caller)
        {
            return [];
        }

        // Explicit shares to the person, their roles or their organization; a tenant-wide or
        // public grant is not "shared with me". Each is resolved, so a block or an ended share
        // keeps it out. With the access index, the candidates are its rows decided on the object
        // itself, contributed entries (IShareEntrySource) included; without it, the stored
        // entries.
        var principals = SharePrincipals.Of(caller).Where(p => p.Kind is SharePrincipalKind.User or SharePrincipalKind.Role or SharePrincipalKind.Organization).ToList();
        var candidates = accessStore is not null
            ? await accessStore.FindGrantedAsync(principals, DateTime.SpecifyKind(clock?.UtcNow ?? DateTime.UtcNow, DateTimeKind.Utc))
            : await store.FindGrantedAsync(principals);
        var shared = new List<ShareableRef>();
        foreach (var candidate in candidates)
        {
            if ((await resolver.ResolveAsync(caller, candidate, cancellationToken)).Allows(AccessLevel.Viewer))
            {
                shared.Add(candidate);
            }
        }

        return shared;
    }

    /// <summary>The caller, when they administer the object's tree (or the tenant's sharing).</summary>
    private async Task<CallerContext> RequireAdminAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        var caller = callers.Current ?? throw new AccessDeniedException(SharingPermissionNames.ManageTenantSharing, "Sharing was asked with no caller: the access gate did not run.");
        if (caller.IsSuperUser)
        {
            return caller;
        }

        if (target.IsTenant)
        {
            await decision.RequireAsync(caller, SharingPermissionNames.ManageTenantSharing, cancellationToken: cancellationToken);
            return caller;
        }

        var resolution = await resolver.ResolveAsync(caller, target, cancellationToken);
        if (resolution.IsInactive || resolution.Level < AccessLevel.Admin)
        {
            // An object the caller cannot view does not exist for them (requirement 68).
            throw new AccessDeniedException(
                "Sharing.Admin",
                resolution.Allows(AccessLevel.Viewer) ? $"Only the administrators of this item's tree may see or change its sharing." : "Not found.");
        }

        return caller;
    }

    private async Task ValidateAsync(CallerContext caller, ShareableRef target, IReadOnlyList<ShareEntry> entries, CancellationToken cancellationToken, bool effectsBelow = false)
    {
        var isRoot = !target.IsTenant && !effectsBelow && await IsRootAsync(target, cancellationToken);
        foreach (var entry in entries)
        {
            if (entry.Principal.Kind != SharePrincipalKind.Anyone && entry.Principal.Kind != SharePrincipalKind.Tenant && string.IsNullOrWhiteSpace(entry.Principal.Id))
            {
                throw new ArgumentException($"A {entry.Principal.Kind} principal needs an id.", nameof(entries));
            }

            if (entry.Principal.OrganizationId is not null && (entry.Principal.Kind != SharePrincipalKind.Role || string.IsNullOrWhiteSpace(entry.Principal.OrganizationId)))
            {
                throw new ArgumentException("Only a role can be limited to an organization.", nameof(entries));
            }

            if (entry.IsBlock)
            {
                continue;
            }

            if (entry.Level == AccessLevel.None)
            {
                throw new ArgumentException("A grant needs an access level.", nameof(entries));
            }

            if (entry.Principal.Kind == SharePrincipalKind.Anyone)
            {
                if (entry.Level > AccessLevel.Viewer)
                {
                    throw new ArgumentException("Anyone can only be granted Viewer.", nameof(entries));
                }

                await decision.RequireAsync(caller, SharingPermissionNames.ShareWithAnyone, cancellationToken: cancellationToken);
            }

            if (entry.Level == AccessLevel.Admin && !isRoot)
            {
                throw new ArgumentException("Admin is granted only on a root object (a drive), never inside it or by a rule.", nameof(entries));
            }

            // A kind may limit who holds Admin on a root (a personal drive: its owner alone),
            // whoever asks, tenant administrators included.
            if (entry.Level == AccessLevel.Admin &&
                kinds.Find(target.Kind) is IShareableAdminEligibility eligibility &&
                await kinds.Find(target.Kind)!.GetAsync(target.Id, cancellationToken) is { } root &&
                !eligibility.MayHoldAdmin(root, entry.Principal))
            {
                throw new ArgumentException("Admin on this root can be held by its owner only.", nameof(entries));
            }
        }
    }

    private async Task<bool> IsRootAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        var kind = kinds.Find(target.Kind) ?? throw new ArgumentException($"'{target.Kind}' is not a shareable kind.", nameof(target));
        var item = await kind.GetAsync(target.Id, cancellationToken) ?? throw new ArgumentException($"'{target}' does not exist.", nameof(target));
        return item.IsRoot;
    }

    private static bool IsAdminGrant(ShareEntry entry) => !entry.IsBlock && entry.Level == AccessLevel.Admin;

    private static string Detail(string detail, string? throughPermission) =>
        throughPermission is null ? detail : $"{detail} (through {throughPermission})";

    /// <summary>
    /// The permission name when <paramref name="target"/> is a root of a kind whose Admins the
    /// tenant assigns (<see cref="IShareableAdminAssignment"/>) and the caller holds that
    /// permission; otherwise null.
    /// </summary>
    private async Task<string?> AdminAssignerAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        if (target.IsTenant || callers.Current is not { } caller || kinds.Find(target.Kind) is not IShareableAdminAssignment assignment)
        {
            return null;
        }

        var item = await ((IShareableKind)assignment).GetAsync(target.Id, cancellationToken);
        if (item is not { IsRoot: true } || !assignment.AppliesTo(item))
        {
            return null;
        }

        return (await decision.DecideAsync(caller, assignment.AdminAssignmentPermission, cancellationToken: cancellationToken)).IsAllowed
            ? assignment.AdminAssignmentPermission
            : null;
    }

    private async Task<bool> IsAdminAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        try
        {
            await RequireAdminAsync(target, cancellationToken);
            return true;
        }
        catch (AccessDeniedException)
        {
            return false;
        }
    }

    private Task IndexAsync(ShareableRef target, bool narrows, CancellationToken cancellationToken) =>
        index?.ApplyAsync(target, narrows, cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// The new entry can only add viewing access over the old one: a grant of at least Viewer
    /// that replaces nothing, a block, or a grant whose window it contains. Levels beyond
    /// viewing do not matter to the index, which lists; the decision is always resolved live.
    /// </summary>
    public static bool Widens(ShareEntry? previous, ShareEntry entry)
    {
        if (entry.IsBlock || entry.Level < AccessLevel.Viewer)
        {
            return false;
        }

        if (previous is null || previous.IsBlock || previous.Level < AccessLevel.Viewer)
        {
            return true;
        }

        var startsNoLater = entry.Starts is null || (previous.Starts is not null && entry.Starts <= previous.Starts);
        var endsNoEarlier = entry.Ends is null || (previous.Ends is not null && entry.Ends >= previous.Ends);
        return startsNoLater && endsNoEarlier;
    }

    private async Task<ShareDocument> DocumentAsync(ShareableRef target) =>
        await store.GetAsync(target) ?? new ShareDocument { Kind = target.Kind, ObjectId = target.Id };

    private Task RecordAsync(CallerContext caller, string operation, ShareableRef target, string detail, CancellationToken cancellationToken) =>
        auditor.RecordAsync(new AccessEvent(AccessEventKind.Execution, operation, caller, AccessVerdict.Allow, target.ToString(), caller.ScopeSignature, detail), cancellationToken);
}
