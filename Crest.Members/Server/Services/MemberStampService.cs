using Crest.Members.Constants;
using Crest.Members.Indexes;
using Crest.Members.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Users.Models;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// Turns a not-yet-saved user record into a member of an organization: class aspect,
/// member info with the first org binding, and the hierarchy node (deferred). Shared
/// by the explicit member-creation path (MemberService) and the creation stamp that
/// handles portal registration / portal external logins - one definition of "what a
/// new member looks like". Depends on the session only, so it is safe inside the
/// IUserEventHandler chain (an IUserService dependency there would be circular).
/// </summary>
public sealed class MemberStampService(ISession session)
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

        // The USER, not user.UserId: this runs BEFORE CreateUserAsync, and
        // UserStore.CreateAsync is what assigns the id (onto this same instance). A
        // captured string would be empty here, which silently failed every deferred
        // task on the creation path - the deferred-task runner logs and swallows.
        DeferAddHierarchyNode(organizationId, user);
        DeferMemberBound(organizationId, user, isMemberAdmin);
    }

    /// <summary>
    /// Reports the new binding to whoever keeps records keyed by (member, organization)
    /// — see <see cref="IMemberLifecycleHandler"/>. Members knows nothing about what they
    /// do with it; the first-admin flag is what tells a handler this is the organization's
    /// first owner (the moment its groups are worth creating, for instance).
    /// </summary>
    /// <remarks>
    /// Deferred for the same reason the hierarchy write is: this runs before the user
    /// record is first saved, and a handler goes through the content manager, which must
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

    // The hierarchy table is written over a RAW connection with its own transaction;
    // running that while the ambient YesSql session still holds its uncommitted write
    // lock deadlocks SQLite ('database is locked' - verified live 2026-09-09).
    // Deferred tasks run AFTER the scope's session commits, in a fresh scope, which is
    // exactly the ordering the raw write needs.
    /// <summary>
    /// Takes the USER, not its id: on the creation path this is registered before
    /// <c>CreateUserAsync</c> has assigned one, so the id is read when the task RUNS.
    /// Capturing the string instead left every creation-path node unwritten - the
    /// deferred-task runner logs the ArgumentNullException and swallows it, so nothing
    /// surfaced until a second deferred task failed the same way.
    /// </summary>
    public static void DeferAddHierarchyNode(string organizationId, User user)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var userId = user.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var hierarchy = scope.ServiceProvider.GetRequiredService<IUserHierarchyService>();
            await hierarchy.AddAsync(HierarchyRoot.ForOrganization(organizationId), userId);
        });
    }
}
