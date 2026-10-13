using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crest.Access;
using Crest.Modules;
using Crest.Services;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/auth")]
public sealed class AuthController(
    LoginService loginService,
    ICallerContextAccessor callers,
    Crest.Users.Services.IUserService userService,
    IEnumerable<Crest.Users.Events.ILogoutFormEvent> logoutFormEvents,
    Microsoft.Extensions.Logging.ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>The request's caller as the gate built it: identity and roles, never the claims.</summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        if (callers.Current is not { IsAuthenticated: true } caller)
        {
            return Ok(new AuthUser(false, null, []));
        }

        return Ok(new AuthUser(true, caller.UserName, caller.Roles.ToArray()));
    }

    // The admin login shell's JSON adapter over the shared LoginService flow (the
    // full stock ILoginFormEvent sequence, veto translated into the 401 payload).
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await loginService.LoginAsync(ControllerContext, request.UserName, request.Password, request.RememberMe);
        if (!result.Succeeded)
        {
            return Unauthorized(result.Refusal);
        }

        // The gate built this request's caller before the sign-in; the roles come from the
        // user just signed in, which the next request's caller will carry.
        return Ok(new AuthUser(
            true,
            result.User!.UserName,
            (result.User as Crest.Users.Models.User)?.RoleNames.ToArray() ?? []));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // The same logout events as the stock LogOff (the audit trail's sign-out record among
        // them, docs/audit.md › step 2): before the sign-out, while the session is still there.
        var user = await userService.GetAuthenticatedUserAsync(User);
        if (user is not null)
        {
            await logoutFormEvents.InvokeAsync((handler, user) => handler.LoggingOutAsync(user), user, logger);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        if (user is not null)
        {
            await logoutFormEvents.InvokeAsync((handler, user) => handler.LoggedOutAsync(user), user, logger);
        }

        return Ok(new AuthUser(false, null, []));
    }
}
