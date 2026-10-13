using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.Entities;
using Crest.Members.Constants;
using Crest.Members.Models;
using Crest.Members.Services;
using Crest.Parties.Constants;
using Crest.Parties.Services;
using Crest.Users.Handlers;
using Crest.Users.Models;

namespace Crest.Members.Handlers;

/// <summary>
/// Places every new user in an organization at creation (no user exists outside one).
/// CreatingAsync runs inside UserStore.CreateAsync BEFORE the save, so the binding persists
/// in the same write and the connection index maps it on first save. Flows that build the
/// user themselves (MemberService) stamp BEFORE calling create and are left alone. A user
/// created while the request is a portal registration or a portal external login
/// (MemberPortalLoginContext carries the organization) becomes a member of that
/// organization; everything else becomes staff of the tenant organization. An external
/// login that provisions a member also gets its Person, as registration does. Portal
/// provisioning never touches the stock registration/external-login code - the surface
/// decides.
/// </summary>
public class UserClassStampHandler(
    MemberPortalLoginContext portalContext,
    MemberBindingService bindings,
    IContentManager contentManager,
    IPartyUserLinkService partyUserLink) : UserEventHandlerBase
{
    public override async Task CreatingAsync(UserCreateContext context)
    {
        if (context.User is not User user || (user.TryGet<CrestMemberInfo>(out var existing) && existing.Bindings.Count > 0))
        {
            return;
        }

        var portal = await portalContext.ResolveAsync();

        // A portal sign-in that named no organization may sign an existing member in, never
        // create one: no user exists outside an organization, and a portal account is never
        // staff. Refused, not defaulted.
        if (portal is not null && string.IsNullOrWhiteSpace(portal.OrganizationId))
        {
            context.Cancel = true;
            return;
        }

        var userClass = portal is not null ? UserClasses.Member : UserClasses.Staff;

        // UserStore has assigned the id before the creating handlers run, so the node is
        // written now, in the same unit of work. The handler chain logs and swallows
        // exceptions, so a failure must also cancel the creation itself.
        try
        {
            if (userClass == UserClasses.Staff)
            {
                await bindings.StampStaffAsync(user);
                return;
            }

            var organizationId = portal!.OrganizationId!;
            var personId = portal.PersonId;
            if (string.IsNullOrWhiteSpace(personId))
            {
                // Registration created the Person before the account; an external login
                // reaches here without one, so the party the account IS is created now.
                personId = await CreatePersonAsync(user);
            }

            await bindings.StampAsync(user, organizationId, userClass, personId, roles: null);
            await bindings.AddHierarchyNodeAsync(organizationId, user.UserId);
        }
        catch
        {
            context.Cancel = true;
            throw;
        }
    }

    private async Task<string> CreatePersonAsync(User user)
    {
        var person = await contentManager.NewAsync(PartiesConstants.ContentTypes.Person);
        person.DisplayText = string.IsNullOrWhiteSpace(user.UserName) ? user.Email ?? string.Empty : user.UserName;
        ((System.Text.Json.Nodes.JsonObject)person.Content)["TitlePart"] = new System.Text.Json.Nodes.JsonObject { ["Title"] = person.DisplayText };
        await contentManager.CreateAsync(person, VersionOptions.Published);
        await partyUserLink.LinkPortalUserAsync(person.ContentItemId, user.UserId);
        return person.ContentItemId;
    }
}
