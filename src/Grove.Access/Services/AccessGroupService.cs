using Grove.Access.Store;
using Grove.Infrastructure.Settings;
using Grove.Entities;
using YesSql;

namespace Grove.Access.Services;

/// <summary>
/// The one group store (docs/permissions.md › Layers): tenant groups and each organization's
/// own, in YesSql documents with the group and member indexes. Every write is a rights
/// write: the touched users' projections are rebuilt and the permission version bumps.
/// </summary>
public sealed class AccessGroupService(
    ISession session,
    Grove.Entities.IIdGenerator ids,
    IPermissionVersion permissionVersion,
    IOrganizationAccessDefaultsService defaults,
    IUserAccessProjector projector,
    IServiceProvider services) : IAccessGroupService
{
    /// <summary>
    /// The owner keeps their org-admin role whoever writes (docs/permissions.md › The
    /// organization owner): a change to an organization's groups that would leave its owner in
    /// no group holding the owner role is refused. Ownership is resolved when used (Members
    /// owns it and reaches this service), never in the constructor.
    /// </summary>
    private async Task GuardOwnerAsync(string organizationId, Func<AccessGroup, AccessGroup?> after, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(organizationId) || services.GetService(typeof(IOrganizationOwnership)) is not IOrganizationOwnership ownership
            || await ownership.GetOwnerAsync(organizationId, cancellationToken) is not { } owner)
        {
            return;
        }

        var holds = (await session.Query<AccessGroup, AccessGroupIndex>(index => index.OrganizationId == organizationId).ListAsync(cancellationToken))
            .Select(after)
            .Any(group => group is not null
                && group.UserIds.Contains(owner, StringComparer.Ordinal)
                && group.Roles.Contains(ownership.OwnerRoleName, StringComparer.OrdinalIgnoreCase));
        if (!holds)
        {
            throw new OwnerProtectionException($"The organization's owner must keep the {ownership.OwnerRoleName} role; transfer ownership first.");
        }
    }

    public async Task<IReadOnlyList<AccessGroupModel>> ListAsync(AccessReach reach, CancellationToken cancellationToken = default)
    {
        var key = reach.Key;
        var groups = await session.Query<AccessGroup, AccessGroupIndex>(index => index.OrganizationId == key).ListAsync(cancellationToken);
        return groups.Select(ToModel).OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<AccessGroupModel?> GetAsync(string groupId, CancellationToken cancellationToken = default) =>
        await FindAsync(groupId, cancellationToken) is { } group ? ToModel(group) : null;

    public async Task<AccessGroupModel> CreateAsync(AccessReach reach, AccessGroupWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(write.Name);
        var group = new AccessGroup
        {
            GroupId = ids.GenerateUniqueId(),
            OrganizationId = reach.Key,
        };
        Apply(group, write);
        await session.SaveAsync(group);
        await permissionVersion.BumpAsync(cancellationToken);
        return ToModel(group);
    }

    public async Task<AccessGroupModel?> UpdateAsync(string groupId, AccessGroupWrite write, CancellationToken cancellationToken = default)
    {
        var group = await FindAsync(groupId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var changed = new AccessGroup { GroupId = group.GroupId, OrganizationId = group.OrganizationId, UserIds = group.UserIds, TemplateId = group.TemplateId };
        Apply(changed, write);
        await GuardOwnerAsync(group.OrganizationId, candidate => candidate.GroupId == group.GroupId ? changed : candidate, cancellationToken);

        Apply(group, write);
        await session.SaveAsync(group);
        await TouchedAsync(group.UserIds, cancellationToken);
        return ToModel(group);
    }

    public async Task<bool> DeleteAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var group = await FindAsync(groupId, cancellationToken);
        if (group is null)
        {
            return false;
        }

        await GuardOwnerAsync(group.OrganizationId, candidate => candidate.GroupId == group.GroupId ? null : candidate, cancellationToken);
        session.Delete(group);
        await TouchedAsync(group.UserIds, cancellationToken);
        return true;
    }

    public async Task AddUserAsync(string groupId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var group = await FindAsync(groupId, cancellationToken) ?? throw new InvalidOperationException("Unknown group.");
        if (group.UserIds.Contains(userId, StringComparer.Ordinal))
        {
            return;
        }

        group.UserIds.Add(userId);
        await session.SaveAsync(group);
        await TouchedAsync([userId], cancellationToken);
    }

    public async Task RemoveUserAsync(string groupId, string userId, CancellationToken cancellationToken = default)
    {
        var group = await FindAsync(groupId, cancellationToken);
        if (group is null || !group.UserIds.Contains(userId, StringComparer.Ordinal))
        {
            return;
        }

        await GuardOwnerAsync(group.OrganizationId, candidate => candidate.GroupId == group.GroupId
            ? new AccessGroup { GroupId = group.GroupId, Roles = group.Roles, UserIds = group.UserIds.Where(id => id != userId).ToList() }
            : candidate, cancellationToken);
        group.UserIds.RemoveAll(id => string.Equals(id, userId, StringComparison.Ordinal));
        await session.SaveAsync(group);
        await TouchedAsync([userId], cancellationToken);
    }

    public async Task<IReadOnlyList<AccessGroupModel>> GroupsOfAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default)
    {
        var key = reach.Key;
        var groups = await session.Query<AccessGroup, AccessGroupMemberIndex>(index => index.UserId == userId && index.OrganizationId == key).ListAsync(cancellationToken);
        return groups.Select(ToModel).OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<AccessGroupModel>> CloneDefaultsAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        var templateIds = (await defaults.GetAsync(cancellationToken)).DefaultGroupIds;
        if (templateIds.Count == 0)
        {
            return [];
        }

        var existing = (await session.Query<AccessGroup, AccessGroupIndex>(index => index.OrganizationId == organizationId).ListAsync(cancellationToken))
            .Select(group => group.TemplateId)
            .Where(template => template is not null)
            .ToHashSet(StringComparer.Ordinal);
        var created = new List<AccessGroupModel>();
        foreach (var templateId in templateIds)
        {
            if (existing.Contains(templateId) || await FindAsync(templateId, cancellationToken) is not { OrganizationId: "" } template)
            {
                continue;
            }

            // The roles come along, the users do not: the organization's admins fill the group.
            var clone = new AccessGroup
            {
                GroupId = ids.GenerateUniqueId(),
                OrganizationId = organizationId,
                Name = template.Name,
                Description = template.Description,
                Roles = [.. template.Roles],
                TemplateId = template.GroupId,
            };
            await session.SaveAsync(clone);
            created.Add(ToModel(clone));
        }

        return created;
    }

    public async Task RemoveUserFromReachAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default)
    {
        var key = reach.Key;
        var groups = await session.Query<AccessGroup, AccessGroupMemberIndex>(index => index.UserId == userId && index.OrganizationId == key).ListAsync(cancellationToken);
        var touched = false;
        foreach (var group in groups)
        {
            if (group.UserIds.RemoveAll(id => string.Equals(id, userId, StringComparison.Ordinal)) > 0)
            {
                await session.SaveAsync(group);
                touched = true;
            }
        }

        if (touched)
        {
            await TouchedAsync([userId], cancellationToken);
        }
    }

    public async Task RemoveOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var users = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in await session.Query<AccessGroup, AccessGroupIndex>(index => index.OrganizationId == organizationId).ListAsync(cancellationToken))
        {
            users.UnionWith(group.UserIds);
            session.Delete(group);
        }

        foreach (var overrides in await session.Query<UserAccessOverrides, UserAccessOverrideIndex>(index => index.OrganizationId == organizationId).ListAsync(cancellationToken))
        {
            users.Add(overrides.UserId);
            session.Delete(overrides);
        }

        foreach (var projection in await session.Query<UserAccessProjection, UserAccessIndex>(index => index.OrganizationId == organizationId).ListAsync(cancellationToken))
        {
            session.Delete(projection);
        }

        if (users.Count > 0)
        {
            await TouchedAsync(users, cancellationToken);
        }
    }

    private async Task<AccessGroup?> FindAsync(string groupId, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(groupId)
            ? null
            : await session.Query<AccessGroup, AccessGroupIndex>(index => index.GroupId == groupId).FirstOrDefaultAsync(cancellationToken);

    private async Task TouchedAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        await permissionVersion.BumpAsync(cancellationToken);
        foreach (var userId in userIds.Distinct(StringComparer.Ordinal))
        {
            await projector.RebuildUserAsync(userId, cancellationToken);
        }
    }

    private static void Apply(AccessGroup group, AccessGroupWrite write)
    {
        group.Name = write.Name.Trim();
        group.Description = string.IsNullOrWhiteSpace(write.Description) ? null : write.Description.Trim();
        group.Roles = write.Roles.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static AccessGroupModel ToModel(AccessGroup group) =>
        new(group.GroupId, group.Name, group.Description, AccessReach.FromKey(group.OrganizationId), group.Roles, group.UserIds, group.TemplateId);
}

/// <summary>The overrides store: one document per (user, reach); a same-level conflict is refused.</summary>
public sealed class UserAccessOverrideService(ISession session, IPermissionVersion permissionVersion, IUserAccessProjector projector, IServiceProvider services) : IUserAccessOverrideService
{
    public async Task<IReadOnlyList<UserAccessOverride>> GetAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default) =>
        (await FindAsync(userId, reach, cancellationToken))?.Entries.ToArray() ?? [];

    public async Task SetAsync(string userId, AccessReach reach, IReadOnlyList<UserAccessOverride> overrides, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var cleaned = overrides
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => entry with { Name = entry.Name.Trim() })
            .DistinctBy(entry => (entry.Level, entry.Effect, Name: entry.Name.ToUpperInvariant()))
            .ToList();
        if (AccessResolution.Conflict(cleaned) is { } conflict)
        {
            throw conflict;
        }

        // No override takes the owner's org-admin role away (docs/permissions.md › The organization owner).
        if (!reach.IsTenant
            && services.GetService(typeof(IOrganizationOwnership)) is IOrganizationOwnership ownership
            && cleaned.Any(entry => entry.Level == OverrideLevel.Role && entry.Effect == OverrideEffect.Deny && string.Equals(entry.Name, ownership.OwnerRoleName, StringComparison.OrdinalIgnoreCase))
            && await ownership.GetOwnerAsync(reach.OrganizationId!, cancellationToken) == userId)
        {
            throw new OwnerProtectionException($"The organization's owner must keep the {ownership.OwnerRoleName} role; transfer ownership first.");
        }

        var document = await FindAsync(userId, reach, cancellationToken);
        if (cleaned.Count == 0)
        {
            if (document is null)
            {
                return;
            }

            session.Delete(document);
        }
        else
        {
            document ??= new UserAccessOverrides { UserId = userId, OrganizationId = reach.Key };
            document.Entries = cleaned;
            await session.SaveAsync(document);
        }

        await permissionVersion.BumpAsync(cancellationToken);
        await projector.RebuildUserAsync(userId, cancellationToken);
    }

    private Task<UserAccessOverrides?> FindAsync(string userId, AccessReach reach, CancellationToken cancellationToken)
    {
        var key = reach.Key;
        return session.Query<UserAccessOverrides, UserAccessOverrideIndex>(index => index.UserId == userId && index.OrganizationId == key).FirstOrDefaultAsync(cancellationToken)!;
    }
}

/// <summary>The organization defaults, as site settings; a change is a rights write.</summary>
public sealed class OrganizationAccessDefaultsService(Grove.Infrastructure.Settings.ISiteService siteService, IPermissionVersion permissionVersion) : IOrganizationAccessDefaultsService
{
    public async Task<OrganizationAccessDefaults> GetAsync(CancellationToken cancellationToken = default) =>
        await siteService.GetSettingsAsync<OrganizationAccessDefaults>();

    public async Task SetAsync(OrganizationAccessDefaults defaults, CancellationToken cancellationToken = default)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        site.Put(new OrganizationAccessDefaults
        {
            DefaultGroupIds = Clean(defaults.DefaultGroupIds, StringComparer.Ordinal),
            AssignableRoles = Clean(defaults.AssignableRoles, StringComparer.OrdinalIgnoreCase),
            OwnerLockedPermissions = Clean(defaults.OwnerLockedPermissions, StringComparer.OrdinalIgnoreCase),
        });
        await siteService.UpdateSiteSettingsAsync(site);
        await permissionVersion.BumpAsync(cancellationToken);
    }

    private static List<string> Clean(IEnumerable<string> values, StringComparer comparer) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(comparer).ToList();
}
