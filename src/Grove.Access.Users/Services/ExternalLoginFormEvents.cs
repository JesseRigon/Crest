using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Services;

/// <summary>
/// With one external provider and <see cref="ExternalLoginOptions.UseExternalProviderIfOnlyOneDefined"/>,
/// the login page sends the browser straight to that provider's challenge
/// (<c>api/grove/auth/external-login</c>), keeping the page's return URL. The remember-me choice
/// is the tenant's persistent-cookie setting, the callback's default.
/// </summary>
public sealed class ExternalLoginFormEvents : LoginFormEventBase
{
    private readonly IOptionsMonitor<ExternalLoginOptions> _externalLoginOptions;
    private readonly SignInManager<IUser> _signInManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExternalLoginFormEvents(
        IOptionsMonitor<ExternalLoginOptions> externalLoginOptions,
        SignInManager<IUser> signInManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _externalLoginOptions = externalLoginOptions;
        _signInManager = signInManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<LoginRefusal> LoggingInAsync()
    {
        if (!_externalLoginOptions.CurrentValue.UseExternalProviderIfOnlyOneDefined)
        {
            return null;
        }

        // When the external provider returned a failure, the callback redirects back to /Login
        // with externalLoginError=true so the user can see the error instead of being challenged again.
        var request = _httpContextAccessor.HttpContext.Request;
        if (request.Query.ContainsKey("externalLoginError"))
        {
            return null;
        }

        var schemes = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        if (schemes.Count != 1)
        {
            return null;
        }

        var url = "~/api/grove/auth/external-login?provider=" + Uri.EscapeDataString(schemes[0].Name);
        if (request.Query.TryGetValue("returnUrl", out var returnUrl) && !string.IsNullOrEmpty(returnUrl))
        {
            url += "&returnUrl=" + Uri.EscapeDataString(returnUrl.ToString());
        }

        return LoginRefusal.RedirectTo(url);
    }
}
