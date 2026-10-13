using Crest.AuditTrail.Models;
using Crest.Entities;
using Crest.Environment.Shell.Scope;
using Crest.Members.AuditTrail;
using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Crest.Parties.Services;
using Crest.Settings;
using Crest.Users.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// The ONE write path of the binding record (ruling 2026-10-06: one home per fact). Every
/// binding write - member creation, portal registration and external-login provisioning,
/// the binding API, class conversion, the rebuild - goes through here, so the rules hold
/// everywhere: one binding per (organization, class); staff bind to the tenant
/// organization and only to it; members bind to it only when the tenant setting allows
/// (default blocked); the first member of an organization becomes its member admin; no
/// user is left outside every organization. The connection index and the hierarchy nodes
/// are projections it keeps in step; <see cref="RebuildAsync"/> recomputes them. Depends
/// on the session only for users (never IUserService), so it is safe inside the
/// IUserEventHandler chain.
/// </summary>
public sealed class MemberBindingService(
    ISession session,
    IUserHierarchyService hierarchy,
    ITenantOrganizationService tenantOrganization,
    ISiteService siteService,
    Crest.Access.IPermissionVersion permissionVersion,
    MemberAuditRecorder audit,
    IEnumerable<Crest.Roles.IDerivedRoleProvider> derivedRoles)
{
    /// <summary>
    /// Puts a binding on a user that is not saved yet (creation paths). The hierarchy node
    /// is written by <see cref="AddHierarchyNodeAsync"/> once the user has its id.
    /// </summary>
    public async Task<MemberOrgBinding> StampAsync(User user, string organizationId, string userClass, string? personId, IReadOnlyList<string>? roles, CancellationToken cancellationToken = default)
    {
        var binding = await NewBindingAsync(user, organizationId, userClass, roles, cancellationToken);
        var info = user.GetOrCreate<CrestMemberInfo>();
        info.PersonId ??= personId;
        info.Bindings.Add(binding);
        user.Put(info);

        if (userClass == UserClasses.Member)
        {
            // The USER, not its id: on the explicit creation path the id is assigned later.
            DeferMemberBound(organizationId, user, binding.IsMemberAdmin);
        }

        return binding;
    }

    /// <summary>Adds a binding to a saved user and saves it with its projections.</summary>
    public async Task<MemberOrgBinding> AddAsync(User user, string organizationId, string userClass, IReadOnlyList<string>? roles, CancellationToken cancellationToken = default)
    {
        var binding = await NewBindingAsync(user, organizationId, userClass, roles, cancellationToken);
        var info = user.GetOrCreate<CrestMemberInfo>();
        info.Bindings.Add(binding);
        user.Put(info);

        await session.SaveAsync(user);
        // A binding is a rights write: every cached caller of this tenant is stale now.
        await permissionVersion.BumpAsync(cancellationToken);
        await AddHierarchyNodeAsync(organizationId, user.UserId, cancellationToken);

        if (userClass == UserClasses.Member)
        {
            DeferMemberBound(organizationId, user, binding.IsMemberAdmin);
        }

        await RecordAsync(MemberAuditTrailEventConfiguration.BindingAdded, user, organizationId, binding.Roles, binding.IsMemberAdmin);
        return binding;
    }

    /// <summary>Removes the (organization, class) binding. Refused when it is the user's
    /// last one: no user exists outside an organization.</summary>
    public async Task RemoveAsync(User user, string organizationId, string userClass, CancellationToken cancellationToken = default)
    {
        var info = user.GetOrCreate<CrestMemberInfo>();
        var target = info.Bindings.FirstOrDefault(binding => binding.OrganizationId == organizationId && binding.Class == userClass)
            ?? throw new InvalidOperationException($"The user holds no {userClass} binding to this organization.");

        if (info.Bindings.Count == 1)
        {
            throw new InvalidOperationException("This is the user's last organization; a user cannot exist outside every organization.");
        }

        info.Bindings.Remove(target);
        user.Put(info);
        await session.SaveAsync(user);
        await permissionVersion.BumpAsync(cancellationToken);

        // The node goes when no binding to that organization is left (a staff member of the
        // tenant organization who was also its member keeps their place).
        if (!info.Bindings.Any(binding => binding.OrganizationId == organizationId))
        {
            await hierarchy.RemoveNodeAsync(organizationId, user.UserId, cancellationToken);
        }

        if (userClass == UserClasses.Member)
        {
            DeferMemberUnbound(organizationId, user.UserId);
        }

        await RecordAsync(MemberAuditTrailEventConfiguration.BindingRemoved, user, organizationId, [], false);
    }

    /// <summary>
    /// Writes the user's hierarchy node INLINE in the unit of work, so it commits or rolls
    /// back with the user record. A failure cancels the unit of work and rethrows - a
    /// binding without its node is never committed.
    /// </summary>
    public async Task AddHierarchyNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await hierarchy.EnsureNodeAsync(organizationId, userId, cancellationToken);
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
    }

    /// <summary>
    /// Binds a user being created as staff of the tenant organization. When setup has not
    /// created the organization yet (the setup admin can arrive before the deferred task
    /// that creates it), the binding is deferred to the end of the scope rather than
    /// creating content inside the user-creation handler; the user then belongs to the
    /// tenant organization before anyone can sign in.
    /// </summary>
    public async Task StampStaffAsync(User user, CancellationToken cancellationToken = default)
    {
        if (await tenantOrganization.GetIdAsync(cancellationToken) is { } organizationId)
        {
            await StampAsync(user, organizationId, UserClasses.Staff, personId: null, roles: null, cancellationToken);
            await AddHierarchyNodeAsync(organizationId, user.UserId, cancellationToken);
            return;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var userId = user.UserId;
            var stored = await services.GetRequiredService<ISession>()
                .Query<User, Crest.Users.Indexes.UserIndex>(index => index.UserId == userId)
                .FirstOrDefaultAsync();
            if (stored is null || UserClassSet.Has(stored, UserClasses.Staff))
            {
                return;
            }

            var bindings = services.GetRequiredService<MemberBindingService>();
            await bindings.AddAsync(stored, await bindings.TenantOrganizationIdAsync(), UserClasses.Staff, roles: null);
        });
    }

    /// <summary>The tenant organization's id, created when setup has not yet done so.</summary>
    public Task<string> TenantOrganizationIdAsync(CancellationToken cancellationToken = default) =>
        tenantOrganization.EnsureAsync(cancellationToken);

    /// <summary>
    /// Why a member binding to <paramref name="organizationId"/> is refused, or null. One
    /// source of truth for "may members be bound to the tenant's organization": Parties
    /// says whether it is that organization, the tenant setting says whether it is allowed
    /// (default: blocked). Endpoints that want a clean refusal before they start (portal
    /// registration) ask this; every write asks it again.
    /// </summary>
    public async Task<string?> MemberRefusalAsync(string organizationId, CancellationToken cancellationToken = default) =>
        await tenantOrganization.IsTenantOrganizationAsync(organizationId, cancellationToken)
        && !(await siteService.GetSettingsAsync<MembersSettings>()).AllowMembersOnTenantOrganization
            ? "Members cannot be bound to the tenant's own organization."
            : null;

    public async Task<bool> IsFirstMemberOfOrganizationAsync(string organizationId, CancellationToken cancellationToken = default) =>
        await session.QueryIndex<UserConnectionIndex>(index => index.OrganizationId == organizationId && index.Class == UserClasses.Member)
            .CountAsync(cancellationToken) == 0;

    /// <summary>
    /// Recomputes every projection from the binding records: re-saves each user (its
    /// connection rows), places a user with no binding at all as staff of the tenant
    /// organization, gives every binding its hierarchy node and drops nodes without a
    /// binding, then rebuilds each organization's ancestor lists. Drift repair, and the
    /// migration that populates existing data.
    /// </summary>
    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var tenantOrganizationId = await TenantOrganizationIdAsync(cancellationToken);
        var users = await session.Query<User>().ListAsync(cancellationToken);
        var organizations = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        foreach (var user in users)
        {
            var info = user.GetOrCreate<CrestMemberInfo>();
            if (info.Bindings.Count == 0)
            {
                info.Bindings.Add(new MemberOrgBinding { OrganizationId = tenantOrganizationId, Class = UserClasses.Staff });
            }

            user.Put(info);
            await session.SaveAsync(user);

            var bound = info.Bindings.Select(binding => binding.OrganizationId).ToHashSet(StringComparer.Ordinal);
            foreach (var organizationId in bound)
            {
                await hierarchy.EnsureNodeAsync(organizationId, user.UserId, cancellationToken);
                organizations.Add(organizationId);
            }

            foreach (var node in await hierarchy.GetNodesForUserAsync(user.UserId, cancellationToken))
            {
                if (!bound.Contains(node.OrganizationId))
                {
                    await hierarchy.RemoveNodeAsync(node.OrganizationId, user.UserId, cancellationToken);
                    organizations.Add(node.OrganizationId);
                }
            }

            count++;
        }

        await session.SaveChangesAsync(cancellationToken);
        foreach (var organizationId in organizations)
        {
            await hierarchy.RebuildAsync(organizationId, cancellationToken);
        }

        await permissionVersion.BumpAsync(cancellationToken);
        return count;
    }

    private async Task<MemberOrgBinding> NewBindingAsync(User user, string organizationId, string userClass, IReadOnlyList<string>? roles, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        if (userClass is not (UserClasses.Staff or UserClasses.Member))
        {
            throw new InvalidOperationException($"'{userClass}' is not a user class.");
        }

        if (user.TryGet<CrestMemberInfo>(out var existing)
            && existing.Bindings.Any(binding => binding.OrganizationId == organizationId && binding.Class == userClass))
        {
            throw new InvalidOperationException($"The user is already bound to this organization as {userClass}.");
        }

        if (userClass == UserClasses.Staff)
        {
            if (!await tenantOrganization.IsTenantOrganizationAsync(organizationId, cancellationToken))
            {
                throw new InvalidOperationException("Staff belong to the tenant's own organization only.");
            }

            return new MemberOrgBinding { OrganizationId = organizationId, Class = UserClasses.Staff };
        }

        if (await MemberRefusalAsync(organizationId, cancellationToken) is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }

        // A binding's roles are assigned roles: a derived (business) role is computed from a
        // relationship and never assigned (docs/sharing.md requirement 45).
        if (roles?.FirstOrDefault(role => Crest.Roles.DerivedRoles.IsDerived(derivedRoles, role)) is { } derived)
        {
            throw new Crest.Roles.DerivedRoleAssignmentException(derived);
        }

        return new MemberOrgBinding
        {
            OrganizationId = organizationId,
            Class = UserClasses.Member,
            Roles = (roles ?? [MemberRoleTemplates.Member]).ToList(),
            // The FIRST member of an organization becomes its member admin (ruling 2026-09-09).
            IsMemberAdmin = await IsFirstMemberOfOrganizationAsync(organizationId, cancellationToken),
        };
    }

    private Task RecordAsync(string eventName, User user, string organizationId, IReadOnlyList<string> roles, bool isMemberAdmin) =>
        audit.RecordAsync(
            MemberAuditTrailEventConfiguration.Category,
            eventName,
            user.UserId,
            new MemberAuditTrailEvent { MemberUserId = user.UserId, MemberUserName = user.UserName, OrganizationId = organizationId, Roles = [.. roles], IsMemberAdmin = isMemberAdmin },
            new AuditTrailSubject { RecordId = user.UserId, RecordType = "User", DisplayText = user.UserName, PartyIds = [organizationId] });

    /// <summary>
    /// Reports the new binding to whoever keeps records keyed by (member, organization)
    /// (<see cref="IMemberLifecycleHandler"/>), deferred: a handler goes through the
    /// content manager, which must not re-enter an uncommitted session. The id is read
    /// when the task runs: on the creation path it is assigned after this is registered.
    /// </summary>
    public static void DeferMemberBound(string organizationId, User user, bool isFirstOrganizationAdmin)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var userId = user.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var context = new MemberBoundContext(userId, organizationId, isFirstOrganizationAdmin);
            foreach (var handler in scope.ServiceProvider.GetServices<IMemberLifecycleHandler>())
            {
                await handler.MemberBoundAsync(context);
            }
        });
    }

    /// <summary>Leaving an organization ends the (user, organization) relationship; what
    /// goes with it is for whoever keeps such records (<see cref="IMemberLifecycleHandler"/>).</summary>
    private static void DeferMemberUnbound(string organizationId, string userId)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var context = new MemberUnboundContext(userId, organizationId);
            foreach (var handler in scope.ServiceProvider.GetServices<IMemberLifecycleHandler>())
            {
                await handler.MemberUnboundAsync(context);
            }
        });
    }
}
