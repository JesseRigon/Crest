using Crest.Access;

namespace Crest.Sharing.Services;

/// <summary>
/// Reads and changes sharing as the gated caller (docs/sharing.md, requirements 66, 85, 99).
/// An object's sharing is seen and changed by its tree's Admins (a drive's Drive Admins) and
/// tenant administrators only; tenant-level entries and rules take
/// <see cref="SharingPermissionNames.ManageTenantSharing"/>; a grant to Anyone takes
/// <see cref="SharingPermissionNames.ShareWithAnyone"/>. Every change is recorded. A change
/// applies to the next decision: nothing is cached across requests.
/// </summary>
public sealed class SharingService(
    ICallerContextAccessor callers,
    IAccessDecision decision,
    ISharingResolver resolver,
    IShareStore store,
    ShareableKinds kinds,
    IAccessAuditor auditor) : ISharingService
{
    public async Task<IReadOnlyList<ShareEntry>> GetEntriesAsync(ShareableRef target, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(target, cancellationToken);
        return (await store.GetAsync(target))?.Entries ?? [];
    }

    public async Task SetEntryAsync(ShareableRef target, ShareEntry entry, CancellationToken cancellationToken = default)
    {
        var caller = await RequireAdminAsync(target, cancellationToken);
        await ValidateAsync(caller, target, [entry], cancellationToken);

        var document = await DocumentAsync(target);
        document.Entries.RemoveAll(existing => existing.Principal == entry.Principal);
        document.Entries.Add(entry);
        await store.SaveAsync(document);
        await RecordAsync(caller, "Sharing.SetEntry", target, entry.ToString(), cancellationToken);
    }

    public async Task RemoveEntryAsync(ShareableRef target, SharePrincipal principal, CancellationToken cancellationToken = default)
    {
        var caller = await RequireAdminAsync(target, cancellationToken);
        var document = await store.GetAsync(target);
        if (document is null || document.Entries.RemoveAll(existing => existing.Principal == principal) == 0)
        {
            return;
        }

        await store.SaveAsync(document);
        await RecordAsync(caller, "Sharing.RemoveEntry", target, principal.ToString(), cancellationToken);
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
        // keeps it out.
        var principals = SharePrincipals.Of(caller).Where(p => p.Kind is SharePrincipalKind.User or SharePrincipalKind.Role or SharePrincipalKind.Organization).ToList();
        var shared = new List<ShareableRef>();
        foreach (var candidate in await store.FindGrantedAsync(principals))
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
        }
    }

    private async Task<bool> IsRootAsync(ShareableRef target, CancellationToken cancellationToken)
    {
        var kind = kinds.Find(target.Kind) ?? throw new ArgumentException($"'{target.Kind}' is not a shareable kind.", nameof(target));
        var item = await kind.GetAsync(target.Id, cancellationToken) ?? throw new ArgumentException($"'{target}' does not exist.", nameof(target));
        return item.IsRoot;
    }

    private async Task<ShareDocument> DocumentAsync(ShareableRef target) =>
        await store.GetAsync(target) ?? new ShareDocument { Kind = target.Kind, ObjectId = target.Id };

    private Task RecordAsync(CallerContext caller, string operation, ShareableRef target, string detail, CancellationToken cancellationToken) =>
        auditor.RecordAsync(new AccessEvent(AccessEventKind.Execution, operation, caller, AccessVerdict.Allow, target.ToString(), caller.ScopeSignature, detail), cancellationToken);
}
