using Crest.Members.Constants;
using Crest.Members.Models;
using Crest.Members.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.Entities;
using Crest.Users;
using Crest.Users.Events;
using Crest.Users.Models;

namespace Crest.Members.Handlers;

/// <summary>
/// The login-channel gate (design: docs/members.md §B): each account class has
/// exactly one login surface. A member-class account is refused at the TENANT surfaces
/// even with valid credentials; a staff-class account is refused at the member PORTAL.
/// Which surface the request is comes from <see cref="MemberPortalLoginContext"/>.
/// ValidatingLoginAsync is the one ILoginFormEvent member that can veto an
/// otherwise-valid credential; it fires on the stock local login, all three
/// external-login paths, AND every Crest JSON login (CrestLoginService fires the
/// standard sequence and translates the veto into its 401 payload) - one handler gates
/// every login surface. Modeled on stock DisabledUserLoginFormEvent.
/// </summary>
public class MemberLoginSurfaceGate : LoginFormEventBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;
    private readonly IOptions<Crest.Users.UserOptions> _userOptions;
    private readonly MemberPortalLoginContext _portalContext;

    private readonly IStringLocalizer S;

    public MemberLoginSurfaceGate(
        IHttpContextAccessor httpContextAccessor,
        ITempDataDictionaryFactory tempDataDictionaryFactory,
        IOptions<Crest.Users.UserOptions> userOptions,
        MemberPortalLoginContext portalContext,
        IStringLocalizer<MemberLoginSurfaceGate> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _tempDataDictionaryFactory = tempDataDictionaryFactory;
        _userOptions = userOptions;
        _portalContext = portalContext;
        S = stringLocalizer;
    }

    public override async Task<IActionResult?> ValidatingLoginAsync(IUser user)
    {
        if (user is not User localUser)
        {
            return null;
        }

        var isMember = localUser.TryGet<CrestUserClass>(out var userClass) && userClass.Class == UserClasses.Member;
        var portal = await _portalContext.ResolveAsync();

        string? refusal = null;
        if (portal is null && isMember)
        {
            refusal = S["This account signs in through its organization's member portal."];
        }
        else if (portal is not null && !isMember)
        {
            refusal = S["This account signs in through the tenant login, not the member portal."];
        }
        else if (portal?.OrganizationId is { Length: > 0 } organizationId
            && !(localUser.TryGet<CrestMemberInfo>(out var memberInfo)
                && memberInfo.Bindings.Any(binding => binding.OrganizationId == organizationId)))
        {
            refusal = S["This account is not a member of that organization."];
        }

        if (refusal is null)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext!;

        // An external provider may already have set the correlation cookie; clear it so
        // the refused account is not left with partial authentication state (same care
        // stock DisabledUserLoginFormEvent takes).
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // Same TempData "error_*" channel the stock login GET reads back into ModelState.
        var tempData = _tempDataDictionaryFactory.GetTempData(httpContext);
        tempData["error_member_login"] = refusal;

        // Redirect to the tenant login. The path comes from UserOptions - the same
        // Crest configuration that registered the login route - never a literal.
        var loginPath = "/" + _userOptions.Value.LoginPath;
        if (httpContext.Request.Query.TryGetValue("returnUrl", out var returnUrl))
        {
            loginPath = QueryHelpers.AddQueryString(loginPath, "returnUrl", returnUrl.ToString());
        }

        return new LocalRedirectResult("~" + loginPath);
    }
}
