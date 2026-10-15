using Grove.Access.Members.Constants;
using Grove.Access.Members.Models;
using Grove.Access.Members.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Entities;
using Grove.Access.Users;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;

namespace Grove.Access.Members.Handlers;

/// <summary>
/// The login-channel gate (design: docs/members.md §B): each account class has
/// its login surface. An account without a staff connection is refused at the TENANT
/// surfaces even with valid credentials; one without a member connection at the PORTAL.
/// Which surface the request is comes from <see cref="MemberPortalLoginContext"/>.
/// ValidatingLoginAsync is the one ILoginFormEvent member that can veto an
/// otherwise-valid credential; it fires on the stock local login, all three
/// external-login paths, AND every Grove JSON login (LoginService fires the
/// standard sequence and answers the refusal as its 401 payload) - one handler gates
/// every login surface. Modeled on stock DisabledUserLoginFormEvent.
/// </summary>
public class MemberLoginSurfaceGate : LoginFormEventBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<Grove.Access.Users.UserOptions> _userOptions;
    private readonly MemberPortalLoginContext _portalContext;
    private readonly IOptions<Grove.Routing.MemberOptions> _memberOptions;

    private readonly IStringLocalizer S;

    public MemberLoginSurfaceGate(
        IHttpContextAccessor httpContextAccessor,
        IOptions<Grove.Access.Users.UserOptions> userOptions,
        MemberPortalLoginContext portalContext,
        IOptions<Grove.Routing.MemberOptions> memberOptions,
        IStringLocalizer<MemberLoginSurfaceGate> stringLocalizer)
    {
        _memberOptions = memberOptions;
        _httpContextAccessor = httpContextAccessor;
        _userOptions = userOptions;
        _portalContext = portalContext;
        S = stringLocalizer;
    }

    /// <summary>
    /// A portal sign-in through an external provider finishes in the platform's own callback,
    /// which signs in without the session's organization: re-issue the session with it (the
    /// named organization, else the member's first binding - the same rule as the portal's
    /// password sign-in), so the member shell's organization-free pages (the picker) have the
    /// session's last choice to act in. The portal's own endpoints set it themselves.
    /// </summary>
    public override async Task LoggedInAsync(IUser user)
    {
        if (user is not User localUser || await _portalContext.ResolveAsync() is not { External: true } portal)
        {
            return;
        }

        var organizationId = portal.OrganizationId
            ?? UserClassSet.Bindings(localUser, UserClasses.Member).FirstOrDefault()?.OrganizationId;
        if (organizationId is null)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext!;
        var loginSettings = await httpContext.RequestServices.GetRequiredService<Grove.Infrastructure.Settings.ISiteService>()
            .GetSettingsAsync<LoginSettings>();
        var principal = await httpContext.RequestServices.GetRequiredService<Grove.Access.Users.Services.IUserService>()
            .CreatePrincipalAsync(user);
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties
        {
            // The stock external callback's own rule (no remember-me box on that path).
            IsPersistent = !loginSettings.AllowRememberMe && loginSettings.UsePersistentAuthenticationCookie,
            Items = { [MemberSessionKeys.ActiveOrganization] = organizationId },
        });
    }

    public override async Task<LoginRefusal?> ValidatingLoginAsync(IUser user)
    {
        if (user is not User localUser)
        {
            return null;
        }

        // The side comes from the request, never the user (docs/shells-and-themes.md › Member
        // sessions and sides): the portal is a request the portal's own endpoints marked, an
        // external-login callback that carries the portal mark, or any sign-in made through
        // the member shell (the access gate's side for the request). Everything else is the
        // tenant surface. The check then reads the user's bindings - the record of truth - not
        // a derived class: the admin login takes a user holding a staff binding, the portal a
        // user holding at least one organization binding (to the named organization when one
        // is named). A dual user signs in at either; each side gets only its own grants.
        var httpContext = _httpContextAccessor.HttpContext!;
        var portal = await _portalContext.ResolveAsync();
        var isPortal = portal is not null || Grove.Access.AccessGate.SideOf(httpContext) == Grove.Access.CallerSide.Member;
        var holdsStaff = UserClassSet.Bindings(localUser, UserClasses.Staff).Any();
        var memberBindings = UserClassSet.Bindings(localUser, UserClasses.Member).ToArray();

        string? refusal = null;
        if (!isPortal && !holdsStaff)
        {
            refusal = memberBindings.Length > 0
                ? S["This account signs in through its organization's member portal."]
                : S["This account belongs to no organization."];
        }
        else if (isPortal && memberBindings.Length == 0)
        {
            refusal = S["This account signs in through the tenant login, not the member portal."];
        }
        else if (portal?.OrganizationId is { Length: > 0 } organizationId
            && !memberBindings.Any(binding => binding.OrganizationId == organizationId))
        {
            refusal = S["This account is not a member of that organization."];
        }

        if (refusal is null)
        {
            return null;
        }

        // An external provider may already have set the correlation cookie; clear it so
        // the refused account is not left with partial authentication state (same care
        // stock DisabledUserLoginFormEvent takes).
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // Back to the surface the attempt was made on: the portal's own external-login page
        // (which shows the refusal) or the tenant login. The paths come from the tenant's
        // configuration (MemberOptions, UserOptions) and the member shell's route constants -
        // never a literal.
        if (isPortal)
        {
            var portalPath = "/" + _memberOptions.Value.MemberUrlPrefix + Grove.Themes.Members.MemberRoutePaths.ExternalLogin;
            return LoginRefusal.Because(refusal, "~" + QueryHelpers.AddQueryString(portalPath, "error", refusal));
        }

        var loginPath = QueryHelpers.AddQueryString("/" + _userOptions.Value.LoginPath, "error", refusal);
        if (httpContext.Request.Query.TryGetValue("returnUrl", out var returnUrl))
        {
            loginPath = QueryHelpers.AddQueryString(loginPath, "returnUrl", returnUrl.ToString());
        }

        return LoginRefusal.Because(refusal, "~" + loginPath);
    }
}
