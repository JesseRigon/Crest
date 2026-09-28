using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Crest.Workflows.Security;

/// <summary>
/// The one gate on Elsa's API inside the tenant. Elsa authorizes its FastEndpoints by a
/// <c>permissions</c> claim on the principal, not by Orchard permissions; the upstream
/// integration stamped <c>permissions=*</c> on every user at sign-in, which made any
/// logged-in tenant user a workflow administrator. Here the grant is decided per request,
/// from Orchard's own authorization pipeline (roles, the super user, and every
/// <c>IAuthorizationHandler</c> a module adds - Fruitful's member permission ceiling
/// included), and is added to the principal only for the duration of the request:
/// <list type="bullet">
/// <item>anonymous → 401;</item>
/// <item>authenticated without <see cref="Permissions.ManageWorkflows"/> → 403;</item>
/// <item>non-GET without a valid antiforgery token (Elsa's endpoints validate none; a
/// cookie-authenticated API without it is CSRF-exposed) → 400;</item>
/// <item>otherwise the request proceeds with <c>permissions=*</c> on a second identity.</item>
/// </list>
/// Nothing here is cached in the cookie, so a role change takes effect on the next request.
/// </summary>
public sealed class ElsaApiSecurityMiddleware(RequestDelegate next, ILogger<ElsaApiSecurityMiddleware> logger)
{
    public const string ApiPathPrefix = "/elsa/api";

    // Elsa's claim contract (Elsa.Api.Common PermissionNames): the claim type and the wildcard.
    public const string ElsaPermissionsClaimType = "permissions";
    public const string ElsaAllPermissions = "*";

    public async Task InvokeAsync(HttpContext context, IAuthorizationService authorizationService, IAntiforgery antiforgery)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPathPrefix))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!await authorizationService.AuthorizeAsync(context.User, Permissions.ManageWorkflows))
        {
            logger.LogWarning("Workflow API access denied for '{User}' on {Path}.", context.User.Identity.Name, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Antiforgery token missing or invalid.");
                return;
            }
        }

        context.User.AddIdentity(new ClaimsIdentity(new[] { new Claim(ElsaPermissionsClaimType, ElsaAllPermissions) }, "CrestWorkflows"));
        await next(context);
    }
}
