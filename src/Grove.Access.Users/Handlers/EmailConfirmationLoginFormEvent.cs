using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Handlers;

internal sealed class EmailConfirmationLoginFormEvent : LoginFormEventBase
{
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;
    private readonly UserManager<IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmailConfirmationLoginFormEvent(
        IOptionsMonitor<RegistrationOptions> registrationOptions,
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor
        )
    {
        _registrationOptions = registrationOptions;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<LoginRefusal> ValidatingLoginAsync(IUser user)
    {
        // Require that the users have a confirmed email before they can log on.
        if (!_registrationOptions.CurrentValue.UsersMustValidateEmail || await _userManager.IsEmailConfirmedAsync(user))
        {
            return null;
        }

        // The "confirmation sent" page is the site shell's (step 7: the MVC view is retired).
        var query = _httpContextAccessor.HttpContext.Request.Query.TryGetValue("returnUrl", out var back) && !string.IsNullOrEmpty(back)
            ? "?returnUrl=" + Uri.EscapeDataString(back.ToString())
            : string.Empty;
        return LoginRefusal.RedirectTo("~" + Services.AccountPages.ConfirmEmailSent + query);
    }
}
