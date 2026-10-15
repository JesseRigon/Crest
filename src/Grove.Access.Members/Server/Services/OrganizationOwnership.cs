using Grove.Access;
using Grove.AuditTrail.Models;
using Grove.Entities;
using Grove.Access.Members.AuditTrail;
using Grove.Access.Members.Constants;
using Grove.Access.Members.Indexes;
using Grove.Access.Members.Models;
using Grove.Parties.Services;
using Grove.Access.Users.Indexes;
using Grove.Access.Users.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Indexes;

namespace Grove.Access.Members.Services;

/// <summary>The owner of one organization, recorded on it (docs/permissions.md › The organization owner).</summary>
public sealed class OrganizationOwnerRecord
{
    public string OrganizationId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public DateTime SinceUtc { get; set; }
}

public sealed class OrganizationOwnerIndex : MapIndex
{
    public string OrganizationId { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;
}

public sealed class OrganizationOwnerIndexProvider : IndexProvider<OrganizationOwnerRecord>
{
    public override void Describe(DescribeContext<OrganizationOwnerRecord> context) =>
        context.For<OrganizationOwnerIndex>()
            .Map(record => new OrganizationOwnerIndex { OrganizationId = record.OrganizationId, OwnerUserId = record.OwnerUserId });
}

/// <summary>
/// The organization owner (docs/permissions.md › The organization owner): one record per
/// organization; the first member bound becomes owner and org-admin; a transfer binds an
/// outside user as a member, makes the new owner an org-admin, leaves the previous owner's
/// binding and role in place, and is audited. The binding service and the organization's
/// groups are resolved when used: they reach this service, so a constructor dependency on
/// them would be a resolution cycle.
/// </summary>
public sealed class OrganizationOwnershipService(
    ISession session,
    IServiceProvider services,
    IPermissionVersion permissionVersion,
    MemberAuditRecorder audit,
    TimeProvider clock) : IOrganizationOwnership
{
    public string OwnerRoleName => MemberRoleTemplates.MemberAdministrator;

    public async Task<string?> GetOwnerAsync(string organizationId, CancellationToken cancellationToken = default) =>
        (await FindAsync(organizationId, cancellationToken))?.OwnerUserId;

    public async Task<bool> EnsureOwnerAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(userId);
        if (await FindAsync(organizationId, cancellationToken) is not null)
        {
            return false;
        }

        await session.SaveAsync(new OrganizationOwnerRecord { OrganizationId = organizationId, OwnerUserId = userId, SinceUtc = clock.GetUtcNow().UtcDateTime });
        await permissionVersion.BumpAsync(cancellationToken);
        return true;
    }

    public async Task TransferAsync(string organizationId, string newOwnerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        var user = await session.Query<User, UserIndex>(index => index.UserId == newOwnerUserId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The new owner must be a user of this tenant.");
        var bindings = services.GetRequiredService<MemberBindingService>();

        // Owning implies membership: a user outside the organization is bound to it as a member.
        var info = user.GetOrCreate<GroveMemberInfo>();
        var binding = info.Bindings.FirstOrDefault(candidate => candidate.Class == UserClasses.Member && candidate.OrganizationId == organizationId)
            ?? await bindings.AddAsync(user, organizationId, UserClasses.Member, roles: null, cancellationToken);

        // The new owner becomes an org-admin automatically.
        info = user.GetOrCreate<GroveMemberInfo>();
        var stored = info.Bindings.First(candidate => candidate.Class == UserClasses.Member && candidate.OrganizationId == organizationId);
        if (!stored.IsMemberAdmin)
        {
            stored.IsMemberAdmin = true;
            user.Put(info);
            await session.SaveAsync(user);
        }

        await services.GetRequiredService<MemberOrganizationGroups>().PlaceAsync(organizationId, user.UserId, [OwnerRoleName], cancellationToken);

        var record = await FindAsync(organizationId, cancellationToken);
        var previous = record?.OwnerUserId;
        record ??= new OrganizationOwnerRecord { OrganizationId = organizationId };
        record.OwnerUserId = user.UserId;
        record.SinceUtc = clock.GetUtcNow().UtcDateTime;
        await session.SaveAsync(record);
        await permissionVersion.BumpAsync(cancellationToken);

        await audit.RecordAsync(
            MemberAuditTrailEventConfiguration.Category,
            MemberAuditTrailEventConfiguration.OwnershipTransferred,
            organizationId,
            new MemberAuditTrailEvent { MemberUserId = user.UserId, MemberUserName = user.UserName, OrganizationId = organizationId, IsMemberAdmin = true, PreviousOwnerUserId = previous },
            new AuditTrailSubject { RecordId = organizationId, RecordType = "Organization", PartyIds = [organizationId] });
    }

    private Task<OrganizationOwnerRecord?> FindAsync(string organizationId, CancellationToken cancellationToken) =>
        session.Query<OrganizationOwnerRecord, OrganizationOwnerIndex>(index => index.OrganizationId == organizationId).FirstOrDefaultAsync(cancellationToken)!;
}

/// <summary>
/// Owner-locked permissions (tenant setting, docs/permissions.md › The organization owner): a
/// final denial for everyone acting in an organization but its owner. Whatever the groups and
/// overrides say, only the owner holds them there.
/// </summary>
public sealed class OwnerLockedCeiling(IOrganizationAccessDefaultsService defaults, IOrganizationOwnership ownership) : IAccessCeiling
{
    private HashSet<string>? _locked;
    private readonly Dictionary<string, string?> _owners = new(StringComparer.Ordinal);

    public string? Deny(CallerContext caller, string permission, object? resource = null)
    {
        if (caller.OrganizationId is not { } organizationId)
        {
            return null;
        }

        _locked ??= new HashSet<string>(defaults.GetAsync().GetAwaiter().GetResult().OwnerLockedPermissions, StringComparer.OrdinalIgnoreCase);
        if (!_locked.Contains(permission))
        {
            return null;
        }

        if (!_owners.TryGetValue(organizationId, out var owner))
        {
            owner = ownership.GetOwnerAsync(organizationId).GetAwaiter().GetResult();
            _owners[organizationId] = owner;
        }

        return caller.UserId is not null && string.Equals(caller.UserId, owner, StringComparison.Ordinal)
            ? null
            : $"'{permission}' is held by the organization's owner only.";
    }
}

/// <summary>
/// The hierarchy bound on managing users (docs/permissions.md › Who manages the permission
/// model; Staff over organizations): staff holding <c>ManageOrgMembers</c> at tenant reach manage
/// every organization's members from the admin side; otherwise, within the shell's reach (the tenant on the admin side, the acting organization on
/// the member side), a manager manages the users under them in that reach's hierarchy (the
/// tenant organization's for the tenant) and nobody else; no one manages the owner. The super
/// user manages every user of the reach.
/// </summary>
public sealed class MemberHierarchyReach(
    ISession session,
    IUserHierarchyService hierarchy,
    ITenantOrganizationService tenantOrganization,
    IOrganizationOwnership ownership,
    IMemberService members,
    IAccessDecision? decision = null) : IUserManagementReach
{
    public async Task<IReadOnlySet<string>?> ManagedUsersAsync(CallerContext caller, AccessReach reach, CancellationToken cancellationToken = default)
    {
        var none = new HashSet<string>(StringComparer.Ordinal);
        var organizationId = reach.IsTenant ? await tenantOrganization.GetIdAsync(cancellationToken) : reach.OrganizationId;
        if (organizationId is null || caller.UserId is not { } managerId)
        {
            return none;
        }

        var userClass = reach.IsTenant ? UserClasses.Staff : UserClasses.Member;
        if (caller.IsSuperUser)
        {
            return (await session.QueryIndex<UserConnectionIndex>(index => index.OrganizationId == organizationId && index.Class == userClass).ListAsync(cancellationToken))
                .Select(row => row.UserId)
                .ToHashSet(StringComparer.Ordinal);
        }

        // Staff over organizations (ruling 2026-10-14): on the admin side, staff holding the
        // organization-members permission at tenant reach manage every organization's members
        // without a position in its hierarchy - every member but the owner. Not scoped per
        // organization (noted for later).
        if (!reach.IsTenant && caller.OrganizationId is null && caller.Side == CallerSide.Admin
            && decision is not null
            && (await decision.DecideAsync(caller, OrganizationPermissionNames.ManageOrgMembers, null, cancellationToken)).IsAllowed)
        {
            var organizationMembers = (await members.ListByOrganizationAsync(organizationId, cancellationToken))
                .Select(member => member.UserId)
                .ToHashSet(StringComparer.Ordinal);
            if (await ownership.GetOwnerAsync(organizationId, cancellationToken) is { } organizationOwner)
            {
                organizationMembers.Remove(organizationOwner);
            }

            return organizationMembers;
        }

        // The shell bounds the reach: the admin side manages the tenant's users, the member side
        // the acting organization's.
        if (!string.Equals(caller.OrganizationId, reach.OrganizationId, StringComparison.Ordinal))
        {
            return none;
        }

        var managed = (await hierarchy.GetSubtreeUserIdsAsync(organizationId, managerId, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        managed.Remove(managerId);
        if (!reach.IsTenant && await ownership.GetOwnerAsync(organizationId, cancellationToken) is { } owner)
        {
            managed.Remove(owner);
        }

        return managed;
    }
}
