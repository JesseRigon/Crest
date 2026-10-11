using Crest.Members.Models;
using Crest.Members.Services;
using Crest.Entities;
using Crest.Users.Handlers;
using Crest.Users.Models;

namespace Crest.Members.Handlers;

/// <summary>
/// Stamps the user-class aspect at creation. CreatingAsync runs inside
/// UserStore.CreateAsync BEFORE the save, so the stamp persists in the same write and
/// the class index maps it on first save. Flows that build the user themselves
/// (MemberService) stamp BEFORE calling create and are left alone. A user created by
/// Crest's own registration path while the request is a portal registration or a
/// portal external login (MemberPortalLoginContext carries the organization) becomes a
/// member of that org (hierarchy node included, inline); everything else defaults to staff. Portal provisioning therefore
/// never touches the stock registration/external-login code - the surface decides.
/// </summary>
public class UserClassStampHandler(MemberPortalLoginContext portalContext, MemberStampService stamps) : UserEventHandlerBase
{
    public override async Task CreatingAsync(UserCreateContext context)
    {
        if (context.User is not User user || user.Has<CrestUserClass>())
        {
            return;
        }

        var portal = await portalContext.ResolveAsync();
        if (portal?.OrganizationId is { Length: > 0 } organizationId)
        {
            await stamps.StampMemberAsync(user, organizationId, portal.PersonId, roles: null);

            // UserStore has assigned the id before the creating handlers run, so the node
            // is written now, in the same unit of work. The handler chain logs and
            // swallows exceptions, so a failure must also cancel the creation itself
            // (AddHierarchyNodeAsync has already cancelled the unit of work).
            try
            {
                await stamps.AddHierarchyNodeAsync(organizationId, user.UserId);
            }
            catch
            {
                context.Cancel = true;
                throw;
            }

            return;
        }

        user.Put(new CrestUserClass());
    }
}
