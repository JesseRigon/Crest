using System.Security.Claims;
using Grove.Services;
using Grove.Members.Constants;
using Grove.Members.Models;
using Grove.Members.Services;
using Grove.Parties.Constants;
using Grove.Parties.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.Settings;
using Grove.Users;
using Grove.Users.Models;
using Grove.Users.Services;
using Grove.Users.Events;

namespace Grove.Members.Controllers;

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
[Route("api/grove/members/portal")]
public sealed class MemberPortalController(
    LoginService loginService,
    MemberPortalLoginContext portalContext,
    IMemberService memberService,
    MemberBindingService bindings,
    IIndividualOrganizationService individuals,
    MemberOrganizationDirectory organizationDirectory,
    IUserService users,
    IContentManager contentManager,
    IPartyUserLinkService partyUserLink,
    ISiteService siteService,
    SignInManager<IUser> signInManager,
    UserManager<IUser> userManager,
    UserEmailService userEmails,
    IEnumerable<IPasswordRecoveryFormEvents> recoveryEvents,
    Microsoft.Extensions.Options.IOptions<RegistrationOptions> registrationOptions,
    Microsoft.Extensions.Options.IOptions<Grove.Routing.MemberOptions> memberOptions) : ControllerBase
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

        // The password step passed and the account has two-factor authentication: 202, and the
        // portal's login page finishes with the second step (api/grove/auth/two-factor); the
        // member's organization is chosen on the picker afterwards.
        if (result.RequiresTwoFactor)
        {
            return Accepted();
        }

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

    // Self-registration wraps Grove's own RegisterAsync (registration validation
    // events, moderation, email confirmation - the tenant's RegistrationSettings apply
    // unchanged); the portal mark makes the creation stamp provision a member of the
    // organization, and the Person created here is the party the account IS.
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(PortalRegisterRequest request)
    {
        // No organization named: the registration creates the person's Individual organization
        // (ruling 2026-10-14) together with the person and the account.
        var named = !string.IsNullOrWhiteSpace(request.OrganizationId);
        if (named)
        {
            var organization = await contentManager.GetAsync(request.OrganizationId!, VersionOptions.Published);
            if (organization is not { ContentType: PartiesConstants.ContentTypes.Organization })
            {
                return BadRequest(new PortalRefusal(["Unknown organization."]));
            }

            if (await bindings.MemberRefusalAsync(request.OrganizationId!, HttpContext.RequestAborted) is { } refusal)
            {
                return BadRequest(new PortalRefusal([refusal]));
            }
        }

        var person = await contentManager.NewAsync(PartiesConstants.ContentTypes.Person);
        person.DisplayText = $"{request.FirstName} {request.LastName}".Trim();
        person.Alter<ContentPart>(PartiesConstants.ContentTypes.Person, part =>
        {
            part.Alter<TextField>("FirstName", field => field.Text = request.FirstName?.Trim());
            part.Alter<TextField>("LastName", field => field.Text = request.LastName?.Trim());
        });
        await contentManager.CreateAsync(person, VersionOptions.Published);

        var organizationId = named
            ? request.OrganizationId!
            : (await individuals.EnsureForPersonAsync(person, HttpContext.RequestAborted)).ContentItemId;
        portalContext.MarkPortal(organizationId, person.ContentItemId);

        var errors = new List<string>();
        var user = await users.RegisterAsync(
            new RegisterUserForm { UserName = request.UserName, Email = request.Email, Password = request.Password },
            (key, message) => errors.Add(message));

        if (user is null)
        {
            if (!named && await contentManager.GetAsync(organizationId, VersionOptions.Latest) is { } individual)
            {
                await contentManager.RemoveAsync(individual);
            }

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
                Items = { [MemberSessionKeys.ActiveOrganization] = organizationId },
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

    // Starts an external sign-in FOR the portal: the same challenge Grove's own
    // ExternalAuthenticationsController issues (its callback handles the return), with
    // the organization stamped on the external cookie's properties so the callback's
    // gate and creation stamp treat it as a portal login. A navigation, not a fetch:
    // the challenge redirects to the provider.
    //
    // The organization is optional: without one an existing member signs in with any of their
    // bindings (the portal mark is still set, empty), and an account the provider would create
    // is refused - no user exists outside an organization (UserClassStampHandler). The return
    // URL must be local; the member shell's external-login page passes the member base.
    [HttpGet("external-login")]
    public IActionResult ExternalLogin([FromQuery] string provider, [FromQuery] string? organizationId = null, [FromQuery] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest(new PortalRefusal(["A provider is required."]));
        }

        if (returnUrl is not null && !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = null;
        }

        var redirectUrl = Url.Action("ExternalLoginCallback", "ExternalAuthentications", new { area = "Grove.Users", returnUrl });
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        properties.Items[MemberSessionKeys.PortalOrganization] = organizationId?.Trim() ?? string.Empty;
        return Challenge(properties, provider);
    }

    // Password recovery for members, on the portal (docs/shells-and-themes.md › Member account
    // pages): the tenant's ResetPasswordSettings apply, the email links to the member shell's
    // reset page rather than the tenant's, and the answer never says whether the account
    // exists. Only a member-bound account is sent a portal link; staff recover theirs at the
    // tenant surface.
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPasswordAsync(PortalForgotPasswordRequest request)
    {
        if (!(await siteService.GetSettingsAsync<ResetPasswordSettings>()).AllowResetPassword)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail)
            || await users.GetForgotPasswordUserAsync(request.UserNameOrEmail) is not User user
            || !UserClassSet.Bindings(user, UserClasses.Member).Any())
        {
            return Ok();
        }

        if (registrationOptions.Value.UsersMustValidateEmail && !await userManager.IsEmailConfirmedAsync(user))
        {
            return Ok();
        }

        var memberBase = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/{memberOptions.Value.MemberUrlPrefix}";
        await userEmails.SendPasswordResetAsync(user, (_, code) =>
            $"{memberBase}{Grove.MemberTheme.MemberRoutePaths.ResetPassword}?code={Uri.EscapeDataString(code)}");

        var context = new PasswordRecoveryContext(user);
        foreach (var handler in recoveryEvents)
        {
            await handler.PasswordRecoveredAsync(context);
        }

        return Ok();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordAsync(PortalResetPasswordRequest request)
    {
        if (!(await siteService.GetSettingsAsync<ResetPasswordSettings>()).AllowResetPassword)
        {
            return NotFound();
        }

        string token;
        try
        {
            token = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.Code ?? string.Empty));
        }
        catch (FormatException)
        {
            return BadRequest(new PortalRefusal(["The reset link is not valid."]));
        }

        var errors = new List<string>();
        if (await users.ResetPasswordAsync(request.UserNameOrEmail, token, request.NewPassword, (key, message) => errors.Add(message)))
        {
            if (await users.GetUserAsync(request.UserNameOrEmail) is { } reset)
            {
                var context = new PasswordRecoveryContext(reset);
                foreach (var handler in recoveryEvents)
                {
                    await handler.PasswordResetAsync(context);
                }
            }

            return Ok();
        }

        return BadRequest(new PortalRefusal(errors.Count > 0 ? errors : ["The password could not be reset."]));
    }
}
