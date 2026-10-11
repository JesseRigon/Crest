using Crest.Members.Constants;
using Crest.Members.Models;
using Microsoft.AspNetCore.Identity;
using Crest.Entities;
using Crest.Users;
using Crest.Users.Models;
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
    private readonly MemberStampService _stamps;

    public UserClassConversionService(ISession session, UserManager<IUser> userManager, MemberStampService stamps)
    {
        _session = session;
        _userManager = userManager;
        _stamps = stamps;
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
        // whose node still has children throws and cancels the whole conversion, to be
        // resolved deliberately). Inline: the hierarchy table shares the session's
        // transaction, so the conversion commits or rolls back as one unit.
        foreach (var organizationId in boundOrgs)
        {
            await _stamps.RemoveHierarchyNodesAsync(organizationId, userId, cancellationToken);
        }

        // Kills existing sessions: the next request fails stamp validation and the
        // account signs in again - now through the STAFF surface, per its new class.
        await _userManager.UpdateSecurityStampAsync(user);
    }
}
