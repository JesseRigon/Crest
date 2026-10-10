using System.Security.Claims;
using Crest.Workflows.Registry;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Crest.Security.Permissions;

namespace Crest.Workflows.Security;

/// <summary>
/// The one gate on Crest.Workflows's API inside the tenant. The engine authorizes its
/// FastEndpoints by <c>permissions</c> claims on the principal (one name per endpoint, any
/// of them admits), not by Crest permissions; the upstream integration stamped
/// <c>permissions=*</c> on every user at sign-in, which made any logged-in tenant user a
/// workflow administrator. Here the grant is decided per request, from Crest's own
/// authorization pipeline (roles, the super user, and every <c>IAuthorizationHandler</c> a
/// module adds - the member permission ceiling included), and added to the principal
/// only for the duration of the request, as exactly the engine names each Crest permission
/// maps to (<see cref="EnginePermissions"/>):
/// <list type="bullet">
/// <item>anonymous → 401;</item>
/// <item>authenticated without <see cref="Permissions.ViewWorkflows"/> → 403;</item>
/// <item>non-GET without a valid antiforgery token (the engine's endpoints validate none; a
/// cookie-authenticated API without it is CSRF-exposed) → 400;</item>
/// <item>starting a definition by hand (execute, dispatch) without Run for that definition -
/// its Run list counts - → 403;</item>
/// <item>otherwise the request proceeds; a change the definition's ownership tier or access
/// lists refuse deeper down (<see cref="WorkflowChangeDeniedException"/>) → 403 with the reason.</item>
/// </list>
/// Nothing here is cached in the cookie, so a role change takes effect on the next request.
/// </summary>
public sealed class CrestWorkflowsApiSecurityMiddleware(RequestDelegate next, ILogger<CrestWorkflowsApiSecurityMiddleware> logger)
{
    public const string ApiPathPrefix = "/crest-workflows/api";

    // The engine's claim contract (Crest.Workflows.Api.Common PermissionNames): the claim type and the wildcard.
    public const string CrestWorkflowsPermissionsClaimType = "permissions";
    public const string CrestWorkflowsAllPermissions = "*";

    /// <summary>The engine endpoint permission names each Crest permission grants.</summary>
    public static class EnginePermissions
    {
        public static readonly string[] View =
        [
            "read:*", "read:workflow-definitions", "read:workflow-instances", "read:activity-execution",
            "read:activity-descriptors", "read:activity-descriptors-options", "read:installed-features",
            "read:expression-descriptors", "read:storage-drivers", "read:variable-descriptors", "read:commit-strategies",
            "read:incident-strategies", "read:log-persistence-strategies", "read:workflow-activation-strategies",
        ];

        public static readonly string[] Edit =
        [
            "write:workflow-definitions", "delete:workflow-definitions",
            "actions:workflow-definitions:refresh", "actions:workflow-definitions:reload",
        ];

        public static readonly string[] Publish = ["publish:workflow-definitions", "retract:workflow-definitions"];

        public static readonly string[] Run =
        [
            "exec:workflow-definitions", "exec:tests", "trigger:event", "tasks:complete",
            "write:workflow-instances", "cancel:workflow-instances", "delete:workflow-instances",
        ];

        public static IEnumerable<(Permission Permission, string[] Names)> All =>
        [
            (Permissions.ViewWorkflows, View),
            (Permissions.EditWorkflows, Edit),
            (Permissions.PublishWorkflows, Publish),
            (Permissions.RunWorkflows, Run),
        ];
    }

    public async Task InvokeAsync(HttpContext context, IAuthorizationService authorizationService, IAuthorizationPolicyProvider policyProvider, IAntiforgery antiforgery, IWorkflowDefinitionAccessReader access)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPathPrefix, out var remaining))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!await authorizationService.AuthorizeAsync(context.User, Permissions.ViewWorkflows))
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

        var granted = new List<Claim>();
        foreach (var (permission, names) in EnginePermissions.All)
        {
            if (await authorizationService.AuthorizeAsync(context.User, permission))
            {
                granted.AddRange(names.Select(name => new Claim(CrestWorkflowsPermissionsClaimType, name)));
            }
        }

        // Starting a definition by hand is authorized against that definition (its Run list).
        var started = StartedDefinitionId(remaining);
        if (started is not null && !await authorizationService.AuthorizeAsync(context.User, Permissions.RunWorkflows, await access.GetAsync(started) ?? new WorkflowDefinitionAccessResource(started, [], [])))
        {
            logger.LogWarning("Workflow run denied for '{User}' on definition {DefinitionId}.", context.User.Identity.Name, started);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("You may not run this workflow.");
            return;
        }

        context.User.AddIdentity(new ClaimsIdentity(granted, "CrestWorkflows"));

        // The endpoint's own policy (its permission names) is evaluated here, against the
        // grant just made, so a missing permission is a 403 with a reason. Left to the
        // authorization middleware, the cookie scheme's Forbid would redirect the API call to
        // the access-denied page (an HTML 200 to a fetch).
        var endpoint = context.GetEndpoint();
        var authorizeData = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>();
        if (authorizeData is { Count: > 0 })
        {
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
            if (policy is not null && !(await authorizationService.AuthorizeAsync(context.User, endpoint, policy)).Succeeded)
            {
                logger.LogWarning("Workflow API endpoint denied for '{User}' on {Method} {Path}.", context.User.Identity.Name, context.Request.Method, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("You lack the workflow permission this operation takes.");
                return;
            }
        }

        try
        {
            await next(context);
        }
        catch (WorkflowChangeDeniedException ex) when (!context.Response.HasStarted)
        {
            logger.LogWarning("Workflow change denied for '{User}' on {Path}: {Reason}", context.User.Identity.Name, context.Request.Path, ex.Message);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(ex.Message);
        }
    }

    // /workflow-definitions/{definitionId}/execute|dispatch|bulk-dispatch (the engine's own routes).
    private static string? StartedDefinitionId(PathString path)
    {
        var segments = path.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not { Length: 3 } || !string.Equals(segments[0], "workflow-definitions", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return segments[2].ToLowerInvariant() is "execute" or "dispatch" or "bulk-dispatch" ? Uri.UnescapeDataString(segments[1]) : null;
    }
}
