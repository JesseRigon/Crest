using System.Security.Claims;
using Crest.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Crest.Access.Services;

/// <summary>
/// The platform's <see cref="IAuthorizationService"/> delegates every permission requirement
/// to the one decision. The caller is the gated request's; a principal that is not the
/// caller's (a different user passed explicitly) is built on the fly.
/// </summary>
public sealed class AccessAuthorizationHandler(
    ICallerContextAccessor accessor,
    ICallerContextFactory factory,
    IAccessDecision decision,
    IAccessAuditor auditor,
    IHttpContextAccessor httpContextAccessor) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var requirements = context.Requirements.OfType<PermissionRequirement>().ToArray();
        if (requirements.Length == 0)
        {
            return;
        }

        var caller = await ResolveCallerAsync(context.User);
        foreach (var requirement in requirements)
        {
            if (context.HasSucceeded)
            {
                continue;
            }

            var verdict = await decision.DecideAsync(caller, requirement.Permission.Name, context.Resource);
            if (verdict.IsAllowed)
            {
                context.Succeed(requirement);
                continue;
            }

            // Denials are always recorded (docs/access.md); allows are the operation's to
            // record, since only it knows whether it is a read.
            await auditor.RecordAsync(new AccessEvent(
                AccessEventKind.Decision,
                requirement.Permission.Name,
                caller,
                verdict.Verdict,
                Resource: context.Resource?.ToString(),
                Reason: verdict.Reason));

            if (verdict.IsFinal)
            {
                // A ceiling: sticky, beats every other handler's success.
                context.Fail(new AuthorizationFailureReason(this, verdict.Reason ?? "Ceilinged."));
            }
        }
    }

    private async Task<CallerContext> ResolveCallerAsync(ClaimsPrincipal? principal)
    {
        var current = accessor.Current;
        var principalUserId = principal?.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        if (current is not null)
        {
            if (current.IsSystem || string.Equals(current.UserId, principalUserId, StringComparison.Ordinal))
            {
                return current;
            }

            // One calculation per request: a principal that is not the gated caller's is a
            // defect in the pipeline (a second authentication, a hand-built principal), not a
            // second caller to build. It gets the anonymous caller's rights.
            return CallerContext.Anonymous(current.Tenant, current.Side, permissionVersion: current.PermissionVersion);
        }

        // No gate ran (a host without the shell selector, a test host): build once and hand down.
        var caller = await factory.CreateAsync(new CallerRequest(principal, AccessGate.SideOf(httpContextAccessor.HttpContext), null, null));
        accessor.Current = caller;
        return caller;
    }
}
