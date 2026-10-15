using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Handlers;

internal sealed class UserModerationLoginFormEvent : LoginFormEventBase
{
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;
    private readonly UserManager<IUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserModerationLoginFormEvent(
        IOptionsMonitor<RegistrationOptions> registrationOptions,
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _registrationOptions = registrationOptions;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<LoginRefusal> ValidatingLoginAsync(IUser user)
    {
        if (!_registrationOptions.CurrentValue.UsersAreModerated || user is not User u || u.IsEnabled)
        {
            return Task.FromResult<LoginRefusal>(null);
        }

        // The "registration pending" page is the site shell's.
        var query = _httpContextAccessor.HttpContext.Request.Query.TryGetValue("returnUrl", out var back) && !string.IsNullOrEmpty(back)
            ? "?returnUrl=" + Uri.EscapeDataString(back.ToString())
            : string.Empty;

        return Task.FromResult(LoginRefusal.RedirectTo("~" + Services.AccountPages.RegistrationPending + query));
    }
}
