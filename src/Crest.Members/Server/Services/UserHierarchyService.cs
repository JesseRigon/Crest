using Crest.Locking.Distributed;
using Crest.Members.Indexes;
using Crest.Members.Models;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// The closure-table hierarchy on YesSql, no hand-written SQL: one
/// <see cref="UserHierarchyMembership"/> document per (user, organization) holding the
/// direct managers and the computed ancestors; the closure index is created and queried
/// like every other index. Edge writes are serialized per organization (a distributed
/// lock held until the unit of work is committed), so two writes cannot together create a
/// cycle; each edge change recomputes the ancestors and saves only the documents whose
/// list changed, in one session.
/// </summary>
public sealed class UserHierarchyService(ISession session, IDistributedLock locks) : IUserHierarchyService
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LockExpiration = TimeSpan.FromSeconds(30);

    public async Task EnsureNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        if (await FindAsync(organizationId, userId, cancellationToken) is not null)
        {
            return;
        }

        await session.SaveAsync(new UserHierarchyMembership { UserId = userId, OrganizationId = organizationId });
    }

    public Task RemoveNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default) =>
        WithOrganizationAsync(organizationId, async memberships =>
        {
            if (!memberships.Remove(userId, out var removed))
            {
                return false;
            }

            session.Delete(removed);
            foreach (var other in memberships.Values)
            {
                other.ManagerIds.RemoveAll(id => string.Equals(id, userId, StringComparison.Ordinal));
            }

            return true;
        }, cancellationToken);

    public Task AddManagerAsync(string organizationId, string reportId, string managerId, CancellationToken cancellationToken = default) =>
        WithOrganizationAsync(organizationId, memberships =>
        {
            if (!memberships.TryGetValue(reportId, out var report) || !memberships.ContainsKey(managerId))
            {
                throw new InvalidOperationException("Both users need a place in this organization's hierarchy.");
            }

            if (report.ManagerIds.Contains(managerId, StringComparer.Ordinal))
            {
                return Task.FromResult(false);
            }

            if (HierarchyClosure.WouldCycle(ManagersOf(memberships), reportId, managerId))
            {
                throw new InvalidOperationException("The report is already above that manager; the edge would make a cycle.");
            }

            report.ManagerIds.Add(managerId);
            return Task.FromResult(true);
        }, cancellationToken);

    public Task RemoveManagerAsync(string organizationId, string reportId, string managerId, CancellationToken cancellationToken = default) =>
        WithOrganizationAsync(organizationId, memberships =>
            Task.FromResult(memberships.TryGetValue(reportId, out var report)
                && report.ManagerIds.RemoveAll(id => string.Equals(id, managerId, StringComparison.Ordinal)) > 0),
            cancellationToken);

    public async Task<IReadOnlyList<HierarchyNodeModel>> GetNodesAsync(string organizationId, CancellationToken cancellationToken = default) =>
        [.. (await LoadAsync(organizationId, cancellationToken)).Values.Select(ToModel)];

    public async Task<IReadOnlyList<string>> GetSubtreeUserIdsAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        var below = await session.QueryIndex<UserHierarchyClosureIndex>(index => index.OrganizationId == organizationId && index.AncestorId == userId)
            .ListAsync(cancellationToken);

        return [userId, .. below.Select(row => row.DescendantId).Distinct(StringComparer.Ordinal)];
    }

    public async Task<IReadOnlyList<IReadOnlyList<string>>> GetChainsAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        var memberships = await LoadAsync(organizationId, cancellationToken);
        return memberships.ContainsKey(userId) ? HierarchyClosure.Chains(ManagersOf(memberships), userId) : [];
    }

    public async Task<IReadOnlyList<HierarchyNodeModel>> GetNodesForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        [.. (await session.Query<UserHierarchyMembership, UserHierarchyIndex>(index => index.UserId == userId).ListAsync(cancellationToken)).Select(ToModel)];

    public Task RebuildAsync(string organizationId, CancellationToken cancellationToken = default) =>
        WithOrganizationAsync(organizationId, memberships =>
        {
            foreach (var membership in memberships.Values)
            {
                membership.ManagerIds.RemoveAll(id => !memberships.ContainsKey(id));
            }

            return Task.FromResult(true);
        }, cancellationToken, forceSave: true);

    // The one edge-write path: lock the organization, load its documents, apply the
    // change, recompute every ancestor list, save what changed and commit before the lock
    // is released, so the next writer sees this one's edges.
    private async Task WithOrganizationAsync(
        string organizationId,
        Func<Dictionary<string, UserHierarchyMembership>, Task<bool>> change,
        CancellationToken cancellationToken,
        bool forceSave = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var (locker, locked) = await locks.TryAcquireLockAsync($"crest-hierarchy-{organizationId}", LockTimeout, LockExpiration);
        if (!locked)
        {
            throw new InvalidOperationException("The organization's hierarchy is being changed; try again.");
        }

        await using (locker)
        {
            var memberships = await LoadAsync(organizationId, cancellationToken);
            var before = memberships.ToDictionary(
                entry => entry.Key,
                entry => (Managers: entry.Value.ManagerIds.ToArray(), Ancestors: entry.Value.AncestorIds.ToArray()),
                StringComparer.Ordinal);

            if (!await change(memberships))
            {
                return;
            }

            var ancestors = HierarchyClosure.ComputeAncestors(ManagersOf(memberships));
            foreach (var (userId, membership) in memberships)
            {
                var computed = ancestors.TryGetValue(userId, out var list) ? list : [];
                var changed = !before.TryGetValue(userId, out var previous)
                    || !previous.Ancestors.SequenceEqual(computed, StringComparer.Ordinal)
                    || !previous.Managers.SequenceEqual(membership.ManagerIds, StringComparer.Ordinal);
                membership.AncestorIds = computed;
                if (forceSave || changed)
                {
                    await session.SaveAsync(membership);
                }
            }

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<string, UserHierarchyMembership>> LoadAsync(string organizationId, CancellationToken cancellationToken)
    {
        var documents = await session.Query<UserHierarchyMembership, UserHierarchyIndex>(index => index.OrganizationId == organizationId)
            .ListAsync(cancellationToken);

        var result = new Dictionary<string, UserHierarchyMembership>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            result.TryAdd(document.UserId, document);
        }

        return result;
    }

    private async Task<UserHierarchyMembership?> FindAsync(string organizationId, string userId, CancellationToken cancellationToken) =>
        await session.Query<UserHierarchyMembership, UserHierarchyIndex>(index => index.OrganizationId == organizationId && index.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

    private static Dictionary<string, List<string>> ManagersOf(Dictionary<string, UserHierarchyMembership> memberships) =>
        memberships.ToDictionary(entry => entry.Key, entry => entry.Value.ManagerIds, StringComparer.Ordinal);

    private static HierarchyNodeModel ToModel(UserHierarchyMembership membership) =>
        new(membership.UserId, membership.OrganizationId, [.. membership.ManagerIds], [.. membership.AncestorIds]);
}
