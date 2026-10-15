#nullable enable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Users.Services;
using Grove.Access;
using Grove.Modules;

namespace Grove.Users.Api;

public sealed class AuthApi : ApiContributor
{
    public override string Name => "Auth";

    public override string FeatureId => "Grove.Users";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("auth");
        routes.MapGet("me", ApiHandlers.Handle<AuthHandlers>(h => h.Me()));
        routes.MapPost("login", (HttpContext __http, [FromBody] LoginRequest request) => ApiHandlers.For<AuthHandlers>(__http).Login(request));
        routes.MapPost("logout", ApiHandlers.Handle<AuthHandlers>(h => h.Logout()));
    }
}

public sealed class AuthHandlers(
    LoginService loginService,
    ICallerContextAccessor callers,
    Grove.Users.Services.IUserService userService,
    IEnumerable<Grove.Users.Events.ILogoutFormEvent> logoutFormEvents,
    Microsoft.Extensions.Logging.ILogger<AuthHandlers> logger) : ApiHandlers
{
    /// <summary>The request's caller as the gate built it: identity and roles, never the claims.</summary>
    public IResult Me()
    {
        if (callers.Current is not { IsAuthenticated: true } caller)
        {
            return Ok(new AuthUser(false, null, []));
        }

        return Ok(new AuthUser(true, caller.UserName, caller.Roles.ToArray()));
    }

    // The admin login shell's JSON adapter over the shared LoginService flow (the
    // full stock ILoginFormEvent sequence, veto translated into the 401 payload).
    public async Task<IResult> Login(LoginRequest request)
    {
        var result = await loginService.LoginAsync(new Microsoft.AspNetCore.Mvc.ActionContext(HttpContext, HttpContext.GetRouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()), request.UserName, request.Password, request.RememberMe);
        if (!result.Succeeded)
        {
            return Unauthorized(result.Refusal);
        }

        // The gate built this request's caller before the sign-in; the roles come from the
        // user just signed in, which the next request's caller will carry.
        return Ok(new AuthUser(
            true,
            result.User!.UserName,
            (result.User as Grove.Users.Models.User)?.RoleNames.ToArray() ?? []));
    }

    public async Task<IResult> Logout()
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
