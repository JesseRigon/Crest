using Crest.Members.Constants;
using Crest.Members.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Entities;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// The dedicated class-conversion action (ruling: the class is updatable, but ONLY
/// through this permission-gated action, never the role editor). Member→staff (a
/// hire): bindings end, hierarchy nodes leave the org subtrees, the account keeps its
/// credentials and external logins. The security stamp is updated so existing
/// sessions die and the account re-authenticates on its NEW class's login surface.
/// Staff→member is symmetric mechanically; whether policy allows it is the standing
/// open question - the API exposes only member→staff until ruled.
/// </summary>
public class UserClassConversionService
{
    private readonly ISession _session;
    private readonly UserManager<IUser> _userManager;
    private readonly IUserHierarchyService _hierarchy;

    public UserClassConversionService(ISession session, UserManager<IUser> userManager, IUserHierarchyService hierarchy)
    {
        _session = session;
        _userManager = userManager;
        _hierarchy = hierarchy;
    }

    public async Task ConvertMemberToStaffAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _session.Query<User, Indexes.UserClassIndex>(index => index.UserId == userId && index.Class == UserClasses.Member)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The user is not a member-class account.");

        // End the org bindings; the member aspect's PersonId survives (the person is
        // still the same human - the party link is not a member-only concept).
        var memberInfo = user.GetOrCreate<CrestMemberInfo>();
        var boundOrgs = memberInfo.Bindings.Select(binding => binding.OrganizationId).ToArray();
        memberInfo.Bindings.Clear();
        user.Put(memberInfo);
        user.Alter<CrestUserClass>(userClass => userClass.Class = UserClasses.Staff);

        await _session.SaveAsync(user);

        // The hierarchy nodes leave the org subtrees (leaves only; a converted manager
        // whose node still has children surfaces as an error to resolve deliberately).
        // Deferred: the raw hierarchy write must run after the session commits (SQLite
        // write-lock conflict otherwise - see MemberService).
        ShellScope.AddDeferredTask(async scope =>
        {
            var hierarchy = scope.ServiceProvider.GetRequiredService<IUserHierarchyService>();
            foreach (var organizationId in boundOrgs)
            {
                var root = HierarchyRoot.ForOrganization(organizationId);
                foreach (var node in (await hierarchy.GetNodesForUserAsync(userId))
                    .Where(node => node.RootKind == HierarchyRootKinds.Organization && node.OrganizationId == organizationId))
                {
                    await hierarchy.RemoveAsync(root, node.Id);
                }
            }
        });

        // Kills existing sessions: the next request fails stamp validation and the
        // account signs in again - now through the STAFF surface, per its new class.
        await _userManager.UpdateSecurityStampAsync(user);
    }
}
