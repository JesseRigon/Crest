using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crest.Access;
using Crest.Services;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/auth")]
public sealed class CrestAuthController(CrestLoginService loginService, ICallerContextAccessor callers) : ControllerBase
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

    // The admin login shell's JSON adapter over the shared CrestLoginService flow (the
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
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Ok(new AuthUser(false, null, []));
    }
}
