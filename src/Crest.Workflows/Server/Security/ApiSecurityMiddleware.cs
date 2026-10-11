using System.Security.Claims;
using Crest.Access;
using Crest.Workflows.Contexts;
using Crest.Workflows.Management;
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
/// workflow administrator. Here the grant is decided per request by the one access decision
/// (docs/operations.md step 4) for the request's caller - the one the access gate built when
/// it admitted the request; a request with none is refused - and added to the principal only
/// for the duration of the request, as exactly
/// the engine names each Crest permission maps to (<see cref="EnginePermissions"/>):
/// <list type="bullet">
/// <item>anonymous → 401; a request the path's gate built no caller for → 403;</item>
/// <item>authenticated without <see cref="Permissions.ViewWorkflows"/> → 403;</item>
/// <item>non-GET without a valid antiforgery token (the engine's endpoints validate none; a
/// cookie-authenticated API without it is CSRF-exposed) → 400;</item>
/// <item>starting a definition by hand (execute, dispatch), or changing one of its instances,
/// without Run for that definition - its Run list counts - → 403; the GET form of execute → 405;</item>
/// <item>engine permission claims already on the principal are dropped before the grant;</item>
/// <item>otherwise the request proceeds; a change the definition's ownership tier or access
/// lists refuse deeper down (<see cref="WorkflowChangeDeniedException"/>) → 403 with the reason.</item>
/// </list>
/// Nothing here is cached in the cookie, so a role change takes effect on the next request.
/// </summary>
public sealed class ApiSecurityMiddleware(RequestDelegate next, ILogger<ApiSecurityMiddleware> logger)
{
    public const string ApiPathPrefix = "/crest-workflows/api";

    // The engine's claim contract (Crest.Workflows.Api.Common PermissionNames): the claim type and the wildcard.
    public const string PermissionsClaimType = "permissions";
    public const string AllPermissionsClaim = "*";

    /// <summary>The authentication type of the per-request grant identity.</summary>
    public const string GrantAuthenticationType = "WorkflowsApiGrant";

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

        // Testing an activity runs any definition's draft: an author's operation, not a run.
        public static readonly string[] Edit =
        [
            "write:workflow-definitions", "delete:workflow-definitions",
            "actions:workflow-definitions:refresh", "actions:workflow-definitions:reload", "exec:tests",
        ];

        public static readonly string[] Publish = ["publish:workflow-definitions", "retract:workflow-definitions"];

        public static readonly string[] Run =
        [
            "exec:workflow-definitions", "trigger:event", "tasks:complete",
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

    public async Task InvokeAsync(HttpContext context, WorkflowCallerResolver callers, IAccessDecision decision, IAuthorizationService authorizationService, IAuthorizationPolicyProvider policyProvider, IAntiforgery antiforgery, IWorkflowDefinitionAccessReader access, IWorkflowInstanceStore instances)
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

        // The request path's gate built the caller before routing; a request it did not
        // admit has none and is refused here, never given a caller of its own.
        var caller = callers.Current;
        if (caller is null)
        {
            logger.LogWarning("Workflow API request on {Path} reached the engine gate with no caller.", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!(await decision.DecideAsync(caller, WorkflowsConstants.Permissions.View, cancellationToken: context.RequestAborted)).IsAllowed)
        {
            logger.LogWarning("Workflow API access denied for '{User}' on {Path}.", context.User.Identity.Name, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // The engine's GET form of execute starts a run: a state change no GET may make (a link
        // or an image tag would make it for the signed-in user).
        if (HttpMethods.IsGet(context.Request.Method) && StartedDefinitionId(remaining) is not null)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
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
            if ((await decision.DecideAsync(caller, permission.Name, cancellationToken: context.RequestAborted)).IsAllowed)
            {
                granted.AddRange(names.Select(name => new Claim(PermissionsClaimType, name)));
            }
        }

        // Starting a definition by hand, and changing one of its instances (cancel, delete,
        // variables, import), are authorized against that definition (its Run list).
        var started = StartedDefinitionId(remaining);
        if (started is null && !HttpMethods.IsGet(context.Request.Method) && ChangedInstanceId(remaining) is { } instanceId)
        {
            var instance = await instances.FindAsync(new Management.Filters.WorkflowInstanceFilter { Id = instanceId }, context.RequestAborted);
            started = instance?.DefinitionId;
        }

        if (started is not null && !await MayRunAsync(caller, started, decision, access))
        {
            logger.LogWarning("Workflow run denied for '{User}' on definition {DefinitionId}.", context.User.Identity.Name, started);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("You may not run this workflow.");
            return;
        }

        // Only this grant counts: an engine permission claim the principal already carries (a
        // token minted with one, the wildcard included) is dropped first.
        context.User = new ClaimsPrincipal(context.User.Identities.Select(identity => new ClaimsIdentity(
            identity.Claims.Where(claim => claim.Type != PermissionsClaimType),
            identity.AuthenticationType,
            identity.NameClaimType,
            identity.RoleClaimType)));
        context.User.AddIdentity(new ClaimsIdentity(granted, GrantAuthenticationType));

        // The endpoint's own policy (the engine's permission-claim names) is evaluated here,
        // against the grant just made, so a missing permission is a 403 with a reason. Left to the
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

    // Run on the definition: the decision for the permission, and the definition's Run list as the veto.
    private static async Task<bool> MayRunAsync(CallerContext caller, string definitionId, IAccessDecision decision, IWorkflowDefinitionAccessReader access)
    {
        var resource = await access.GetAsync(definitionId) ?? new WorkflowDefinitionAccessResource(definitionId, [], []);
        return (await decision.DecideAsync(caller, WorkflowsConstants.Permissions.Run, resource)).IsAllowed && resource.Admits(caller, WorkflowsConstants.Permissions.Run);
    }

    // /workflow-instances/{instanceId}[/...] (cancel, delete, variables, import of one instance).
    private static string? ChangedInstanceId(PathString path)
    {
        var segments = path.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is { Length: >= 2 } && string.Equals(segments[0], "workflow-instances", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(segments[1], "bulk", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(segments[1])
            : null;
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
