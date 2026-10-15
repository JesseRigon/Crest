using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Mvc.Utilities;
using Grove.Users.Controllers;
using Grove.Users.Events;
using Grove.Users.Models;

namespace Grove.Users.Handlers;

internal sealed class DisabledUserLoginFormEvent : LoginFormEventBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;

    private readonly IStringLocalizer S;

    public DisabledUserLoginFormEvent(
        IHttpContextAccessor httpContextAccessor,
        ITempDataDictionaryFactory tempDataDictionaryFactory,
        IOptionsMonitor<RegistrationOptions> registrationOptions,
        IStringLocalizer<DisabledUserLoginFormEvent> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _tempDataDictionaryFactory = tempDataDictionaryFactory;
        _registrationOptions = registrationOptions;
        S = stringLocalizer;
    }

    public override async Task<IActionResult> ValidatingLoginAsync(IUser user)
    {
        // Allow the login when the user account is enabled. The default User implementation
        // exposes IsEnabled; non-User implementations are not subject to this check.
        if (user is not User localUser || localUser.IsEnabled)
        {
            return null;
        }

        // When user moderation is enabled, newly registered users are created disabled by design;
        // UserModerationLoginFormEvent handles redirecting them to the RegistrationPending page.
        if (_registrationOptions.CurrentValue.UsersAreModerated)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        // If the user reached this point through an external provider, the external authentication
        // cookie has already been set. Sign it out so the disabled user is not left with a partial
        // authentication state that other handlers could pick up.
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // Back to the login page with the error (a JSON sign-in also reads it from TempData's
        // "error_*" channel, LoginService's veto translation).
        var message = S["The specified user is not allowed to sign in."].Value;
        var tempData = _tempDataDictionaryFactory.GetTempData(httpContext);
        tempData["error_disabled_user"] = message;

        var loginPath = httpContext.RequestServices.GetRequiredService<IOptions<UserOptions>>().Value.LoginPath;
        var query = "?error=" + Uri.EscapeDataString(message);
        if (httpContext.Request.Query.TryGetValue("returnUrl", out var returnUrlValue) && !string.IsNullOrEmpty(returnUrlValue))
        {
            query += "&returnUrl=" + Uri.EscapeDataString(returnUrlValue.ToString());
        }

        return new LocalRedirectResult("~/" + loginPath + query);
    }
}
