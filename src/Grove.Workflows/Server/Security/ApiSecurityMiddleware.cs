using System.Security.Claims;
using Grove.Access;
using Grove.Workflows.Contexts;
using Grove.Workflows.Management;
using Grove.Workflows.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Grove.Security.Permissions;
using Grove.Workflows.Api.Endpoints;

namespace Grove.Workflows.Security;

/// <summary>
/// The one gate on Grove.Workflows's API inside the tenant. The engine authorizes its
/// endpoints by <c>permissions</c> claims on the principal (<see cref="EngineApiPolicies"/>: one
/// set of names per endpoint, any of them admits), not by Grove permissions; the upstream
/// integration stamped <c>permissions=*</c> on every user at sign-in, which made any logged-in
/// tenant user a workflow administrator. Here the grant is decided per request by the one access decision
/// (docs/operations.md step 4) for the request's caller - the one the access gate built when
/// it admitted the request; a request with none is refused - and added to the principal only
/// for the duration of the request, as exactly
/// the engine names each Grove permission maps to (<see cref="EnginePermissions"/>):
/// <list type="bullet">
/// <item>anonymous → 401; a request the path's gate built no caller for → 403;</item>
/// <item>authenticated without <see cref="Permissions.ViewWorkflows"/> → 403;</item>
/// <item>starting a definition by hand (execute, dispatch), changing, cancelling or deleting
/// one of its instances, a bulk action, a bookmark resume, a task completion or an event
/// trigger without Run for the definition(s) reached - the Run list counts - → 403; an
/// instance read (list, journal, variables, export) outside a definition's View list → 403,
/// the instance list narrowed to the definitions the caller may read
/// (<see cref="EngineInstanceAccess"/>); the GET form of execute → 405;</item>
/// <item>engine permission claims already on the principal are dropped before the grant;</item>
/// <item>otherwise the request proceeds; a change the definition's ownership tier or access
/// lists refuse deeper down (<see cref="WorkflowChangeDeniedException"/>) → 403 with the reason.</item>
/// </list>
/// Antiforgery on writes is the registry group's endpoint filter, as for every API.
/// Nothing here is cached in the cookie, so a role change takes effect on the next request.
/// </summary>
public sealed class ApiSecurityMiddleware(RequestDelegate next, ILogger<ApiSecurityMiddleware> logger)
{
    public const string ApiPathPrefix = "/grove-workflows/api";

    // The engine's claim contract (EngineApiPolicies): the claim type and the wildcard.
    public const string PermissionsClaimType = EngineApiPolicies.PermissionsClaimType;
    public const string AllPermissionsClaim = EngineApiPolicies.AllPermissions;

    /// <summary>The authentication type of the per-request grant identity.</summary>
    public const string GrantAuthenticationType = "WorkflowsApiGrant";

    /// <summary>The engine endpoint permission names each Grove permission grants.</summary>
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

    public async Task InvokeAsync(HttpContext context, WorkflowCallerResolver callers, IAccessDecision decision, IAuthorizationService authorizationService, IAuthorizationPolicyProvider policyProvider, IWorkflowDefinitionAccessReader access, EngineInstanceAccess instanceAccess)
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

        var granted = new List<Claim>();
        foreach (var (permission, names) in EnginePermissions.All)
        {
            if ((await decision.DecideAsync(caller, permission.Name, cancellationToken: context.RequestAborted)).IsAllowed)
            {
                granted.AddRange(names.Select(name => new Claim(PermissionsClaimType, name)));
            }
        }

        // Starting a definition by hand is authorized against that definition (its Run list);
        // the instance operations against the definitions they reach (View or Run lists).
        if (StartedDefinitionId(remaining) is { } started && !await MayRunAsync(caller, started, decision, access))
        {
            logger.LogWarning("Workflow run denied for '{User}' on definition {DefinitionId}.", context.User.Identity.Name, started);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("You may not run this workflow.");
            return;
        }

        if (await instanceAccess.CheckAsync(context, remaining, caller, context.RequestAborted) is { } refusal)
        {
            logger.LogWarning("Workflow API request denied for '{User}' on {Method} {Path}: {Reason}", context.User.Identity.Name, context.Request.Method, context.Request.Path, refusal);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync(refusal);
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
        var resource = await access.GetAsync(definitionId) ?? WorkflowDefinitionAccessResource.Open(definitionId);
        return (await decision.DecideAsync(caller, WorkflowsConstants.Permissions.Run, resource)).IsAllowed && resource.Admits(caller, WorkflowsConstants.Permissions.Run);
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
