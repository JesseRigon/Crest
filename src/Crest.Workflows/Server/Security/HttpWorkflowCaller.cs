using Crest.Access;
using Crest.Workflows.Contexts;
using Crest.Workflows.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Security;

/// <summary>
/// An HTTP-endpoint workflow acts as the request's caller (docs/operations.md step 4): the
/// actor goes into the run input before the engine's HTTP middleware runs or resumes the
/// flow, from the gated caller when the request path admitted the request, else from the
/// request's principal.
/// </summary>
public sealed class HttpWorkflowActorContributor(ICallerContextAccessor callerContextAccessor, Crest.Environment.Shell.ShellSettings shellSettings) : IHttpWorkflowInputContributor
{
    public Task ContributeAsync(HttpContext httpContext, IDictionary<string, object> input, CancellationToken cancellationToken = default)
    {
        input[WorkflowsConstants.InputKeys.Actor] = callerContextAccessor.Current is { } caller
            ? WorkflowUserContext.From(caller)
            : WorkflowUserContext.From(httpContext.User, shellSettings.Name);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Replaces the engine's authentication-based handler for an HTTP endpoint with
/// <c>Authorize</c> set: the one decision for <see cref="Permissions.RunWorkflows"/> on the
/// definition (its Run list as the resource, as the API gate asks it), for the request's
/// caller. The trigger's <c>Policy</c> input is not consulted: the operation's decision is the
/// policy.
/// </summary>
public sealed class HttpWorkflowEndpointAuthorizationHandler(
    WorkflowCallerResolver callers,
    IAccessDecision decision,
    IWorkflowDefinitionAccessReader access,
    ILogger<HttpWorkflowEndpointAuthorizationHandler> logger) : IHttpEndpointAuthorizationHandler
{
    public async ValueTask<bool> AuthorizeAsync(AuthorizeHttpEndpointContext context)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        var caller = await callers.ForRequestAsync(CallerSide.Site, cancellationToken);
        if (caller is null)
        {
            return false;
        }

        var definitionId = context.Workflow.Identity.DefinitionId;
        var resource = await access.GetAsync(definitionId) ?? new WorkflowDefinitionAccessResource(definitionId, [], []);
        var verdict = await decision.DecideAsync(caller, WorkflowsConstants.Permissions.Run, resource, cancellationToken);
        // The decision answers the permission; the definition's Run list is the veto beside it.
        var allowed = verdict.IsAllowed && resource.Admits(caller, WorkflowsConstants.Permissions.Run);
        if (!allowed)
        {
            logger.LogWarning("HTTP workflow {Definition} denied for '{Caller}': {Reason}", definitionId, caller.UserName ?? "anonymous", verdict.Reason ?? "not on the definition's Run list");
        }

        return allowed;
    }
}
