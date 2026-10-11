using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Microsoft.Extensions.DependencyInjection;
using Crest.Entities;
using Crest.Environment.Shell.Scope;
using Crest.Users.Models;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// Turns a not-yet-saved user record into a member of an organization: class aspect,
/// member info with the first org binding; the hierarchy node is written once the user
/// has its id (<see cref="AddHierarchyNodeAsync"/>), inside the same unit of work. Shared
/// by the explicit member-creation path (MemberService) and the creation stamp that
/// handles portal registration / portal external logins - one definition of "what a
/// new member looks like". Depends on the session only, so it is safe inside the
/// IUserEventHandler chain (an IUserService dependency there would be circular).
/// </summary>
public sealed class MemberStampService(ISession session, IUserHierarchyService hierarchy)
{
    public async Task StampMemberAsync(User user, string organizationId, string? personId, IReadOnlyList<string>? roles, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        // The FIRST member of an organization becomes its member admin (ruling 2026-09-09).
        var isMemberAdmin = await IsFirstMemberOfOrganizationAsync(organizationId, cancellationToken);

        // Stamped BEFORE the first save so the class/binding indexes map on that save.
        user.Put(new CrestUserClass { Class = UserClasses.Member });
        user.Put(new CrestMemberInfo
        {
            PersonId = personId,
            Bindings =
            [
                new MemberOrgBinding
                {
                    OrganizationId = organizationId,
                    Roles = (roles ?? [MemberRoleTemplates.Member]).ToList(),
                    IsMemberAdmin = isMemberAdmin,
                },
            ],
        });

        // The USER, not user.UserId: on the explicit creation path this runs BEFORE
        // CreateUserAsync, and UserStore.CreateAsync is what assigns the id (onto this
        // same instance), so the id is read when the task runs.
        DeferMemberBound(organizationId, user, isMemberAdmin);
    }

    /// <summary>
    /// Reports the new binding to whoever keeps records keyed by (member, organization)
    /// — see <see cref="IMemberLifecycleHandler"/>. Members knows nothing about what they
    /// do with it; the first-admin flag is what tells a handler this is the organization's
    /// first owner (the moment its groups are worth creating, for instance).
    /// </summary>
    /// <remarks>
    /// Deferred: this runs before the user record is first saved, and a handler goes through the content manager, which must
    /// not re-enter an uncommitted session.
    /// </remarks>
    public static void DeferMemberBound(string organizationId, User user, bool isFirstOrganizationAdmin)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            // Read the id HERE, not at capture time: on the creation path this task is
            // registered before CreateUserAsync has assigned it.
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

    public async Task<bool> IsFirstMemberOfOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var existing = await session.QueryIndex<MemberOrgBindingIndex>(index => index.OrganizationId == organizationId)
            .CountAsync(cancellationToken);
        return existing == 0;
    }

    /// <summary>
    /// Writes the member's hierarchy node for an organization INLINE, on the request's
    /// session transaction (the hierarchy table shares the unit of work), so the node
    /// commits or rolls back with the user record. The user must already have its id:
    /// call it after <c>CreateUserAsync</c>, or from the creating handler, where
    /// UserStore has assigned the id before the handlers run. A failure cancels the
    /// unit of work and rethrows - a member without its node is never committed.
    /// </summary>
    public async Task AddHierarchyNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            await hierarchy.AddAsync(HierarchyRoot.ForOrganization(organizationId), userId, cancellationToken: cancellationToken);
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
    }

    /// <summary>
    /// Removes the user's nodes from one organization's tree, inline in the unit of work
    /// (leaves only - a node with children throws, cancelling the unit of work, so the
    /// binding change it belongs to is not committed half-done).
    /// </summary>
    public async Task RemoveHierarchyNodesAsync(string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = HierarchyRoot.ForOrganization(organizationId);
            foreach (var node in (await hierarchy.GetNodesForUserAsync(userId, cancellationToken))
                .Where(node => node.RootKind == HierarchyRootKinds.Organization && node.OrganizationId == organizationId))
            {
                await hierarchy.RemoveAsync(root, node.Id, cancellationToken);
            }
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
    }
}
