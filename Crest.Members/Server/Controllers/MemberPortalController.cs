using System.Security.Claims;
using Crest.Services;
using Crest.Members.Constants;
using Crest.Members.Models;
using Crest.Members.Services;
using Crest.Parties.Constants;
using Crest.Parties.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace Crest.Members.Controllers;

/// <summary>
/// The member portal's own sign-in surface (design: docs/members.md §B/§G): the
/// same ONE Identity cookie as the tenant (§C), the same stock ILoginFormEvent /
/// registration sequences, but the request is marked as the PORTAL surface so the
/// login-surface gate admits members (and refuses staff) and the creation stamp
/// provisions members (never staff). Anonymous by nature - these are the endpoints
/// behind the [AllowAnonymous] portal pages.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/members/portal")]
public sealed class MemberPortalController(
    CrestLoginService loginService,
    MemberPortalLoginContext portalContext,
    IMemberService memberService,
    MemberOrganizationDirectory organizationDirectory,
    IUserService users,
    IContentManager contentManager,
    IPartyUserLinkService partyUserLink,
    ISiteService siteService,
    SignInManager<IUser> signInManager) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(PortalLoginRequest request)
    {
        portalContext.MarkPortal(string.IsNullOrWhiteSpace(request.OrganizationId) ? null : request.OrganizationId);

        // The gate has already checked the binding when an org was named; otherwise
        // the member's first binding becomes the active org.
        var result = await loginService.LoginAsync(ControllerContext, request.UserName, request.Password, request.RememberMe, properties =>
        {
            if (!string.IsNullOrWhiteSpace(request.OrganizationId))
            {
                properties.Items[MemberSessionKeys.ActiveOrganization] = request.OrganizationId;
            }
        });

        if (!result.Succeeded)
        {
            return Unauthorized(new PortalRefusal(result.Refusal!.Errors, result.Refusal.Redirect));
        }

        var userId = ((User)result.User!).UserId;
        var member = await memberService.GetAsync(userId, HttpContext.RequestAborted);
        var activeOrganization = request.OrganizationId;
        if (string.IsNullOrWhiteSpace(activeOrganization) && member?.Bindings.FirstOrDefault() is { } first)
        {
            activeOrganization = first.OrganizationId;
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, result.Principal!, new AuthenticationProperties
            {
                IsPersistent = request.RememberMe,
                Items = { [MemberSessionKeys.ActiveOrganization] = activeOrganization },
            });
        }

        return Ok(new MemberSessionModel(member, activeOrganization, null, await organizationDirectory.GetAsync(member)));
    }

    // Self-registration wraps Orchard's own RegisterAsync (registration validation
    // events, moderation, email confirmation - the tenant's RegistrationSettings apply
    // unchanged); the portal mark makes the creation stamp provision a member of the
    // organization, and the Person created here is the party the account IS.
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(PortalRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationId))
        {
            return BadRequest(new PortalRefusal(["An organization is required."]));
        }

        var organization = await contentManager.GetAsync(request.OrganizationId, VersionOptions.Published);
        if (organization is not { ContentType: PartiesConstants.ContentTypes.Organization })
        {
            return BadRequest(new PortalRefusal(["Unknown organization."]));
        }

        var person = await contentManager.NewAsync(PartiesConstants.ContentTypes.Person);
        person.DisplayText = $"{request.FirstName} {request.LastName}".Trim();
        person.Alter<ContentPart>(PartiesConstants.ContentTypes.Person, part =>
        {
            part.Alter<TextField>("FirstName", field => field.Text = request.FirstName?.Trim());
            part.Alter<TextField>("LastName", field => field.Text = request.LastName?.Trim());
        });
        await contentManager.CreateAsync(person, VersionOptions.Published);

        portalContext.MarkPortal(request.OrganizationId, person.ContentItemId);

        var errors = new List<string>();
        var user = await users.RegisterAsync(
            new RegisterUserForm { UserName = request.UserName, Email = request.Email, Password = request.Password },
            (key, message) => errors.Add(message));

        if (user is null)
        {
            await contentManager.RemoveAsync(person);
            return BadRequest(new PortalRefusal(errors.Count > 0 ? errors : ["Registration failed."]));
        }

        var userId = ((User)user).UserId;
        await partyUserLink.LinkPortalUserAsync(person.ContentItemId, userId, HttpContext.RequestAborted);

        // RegisterAsync signed the account in unless a registration event cancelled
        // it (moderation, email confirmation). When it did, re-issue the cookie with
        // the active organization - the last sign-in of the request wins.
        var settings = await siteService.GetSettingsAsync<RegistrationSettings>();
        var signedIn = !settings.UsersMustValidateEmail && !settings.UsersAreModerated;
        if (signedIn)
        {
            var principal = await users.CreatePrincipalAsync(user);
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties
            {
                Items = { [MemberSessionKeys.ActiveOrganization] = request.OrganizationId },
            });
        }

        return Ok(new PortalRegisterResult(signedIn, settings.UsersAreModerated, settings.UsersMustValidateEmail));
    }

    [HttpGet("external-providers")]
    public async Task<ActionResult<IReadOnlyList<PortalExternalProvider>>> ExternalProvidersAsync()
    {
        var schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        return Ok(schemes.Select(scheme => new PortalExternalProvider(scheme.Name, scheme.DisplayName ?? scheme.Name)).ToArray());
    }

    // Starts an external sign-in FOR the portal: the same challenge Orchard's own
    // ExternalAuthenticationsController issues (its callback handles the return), with
    // the organization stamped on the external cookie's properties so the callback's
    // gate and creation stamp treat it as a portal login. A navigation, not a fetch:
    // the challenge redirects to the provider.
    [HttpGet("external-login")]
    public IActionResult ExternalLogin([FromQuery] string provider, [FromQuery] string organizationId, [FromQuery] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(organizationId))
        {
            return BadRequest(new PortalRefusal(["A provider and an organization are required."]));
        }

        var redirectUrl = Url.Action("ExternalLoginCallback", "ExternalAuthentications", new { area = "OrchardCore.Users", returnUrl });
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        properties.Items[MemberSessionKeys.PortalOrganization] = organizationId;
        return Challenge(properties, provider);
    }
}
