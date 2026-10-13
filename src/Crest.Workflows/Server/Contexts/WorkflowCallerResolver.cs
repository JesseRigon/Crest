using Crest.Access;
using Crest.Workflows.Activities;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Models;
using Crest.Workflows.Registry;

namespace Crest.Workflows.Contexts;

/// <summary>The gate refused a run: no actor to act as and the definition is not published as system, or the actor claims what it cannot carry.</summary>
public sealed class WorkflowAccessRefusedException(string message) : InvalidOperationException(message);

/// <summary>
/// Builds the caller a workflow burst acts as (docs/operations.md › The four pipelines, the
/// actor rule): a definition published as system runs as the tenant system actor; otherwise
/// the run acts as the actor captured in the instance input, rebuilt into a caller with
/// current rights through <see cref="ICallerContextFactory"/>; a run with neither is refused.
/// Also gives the caller of a request outside the gate (the engine API, an HTTP-endpoint
/// workflow) while the request path's steps 1–6 land: the gated caller when one is set,
/// else one built from the request's principal.
/// </summary>
public sealed class WorkflowCallerResolver(
    ICallerContextFactory factory,
    ICallerContextAccessor accessor,
    WorkflowActorSeal seal,
    IWorkflowInstanceStore instanceStore,
    IWorkflowDefinitionService definitionService)
{
    /// <summary>Whether the definition is published as system (<see cref="WorkflowsConstants.RunsAsSystemProperty"/>).</summary>
    public static bool RunsAsSystem(Workflow workflow) => RunsAsSystem(workflow.CustomProperties);

    public static bool RunsAsSystem(IDictionary<string, object>? properties) =>
        properties is not null && bool.TryParse(WorkflowOwnershipInfo.Text(properties, WorkflowsConstants.RunsAsSystemProperty), out var runsAsSystem) && runsAsSystem;

    /// <summary>Whether the definition accepts anonymous runs (<see cref="WorkflowsConstants.AcceptsAnonymousRunsProperty"/>).</summary>
    public static bool AcceptsAnonymousRuns(Workflow workflow) => AcceptsAnonymousRuns(workflow.CustomProperties);

    public static bool AcceptsAnonymousRuns(IDictionary<string, object>? properties) =>
        properties is not null && bool.TryParse(WorkflowOwnershipInfo.Text(properties, WorkflowsConstants.AcceptsAnonymousRunsProperty), out var accepts) && accepts;

    /// <summary>
    /// Why an anonymous <paramref name="caller"/> may not run <paramref name="workflow"/>, or null
    /// when it may (docs/access.md, Anonymous actors): a signed-in caller, an application and the
    /// system always pass; an anonymous one only for a definition that accepts anonymous runs,
    /// and on a run's first burst only when the run starts at a page-URL HTTP endpoint
    /// (<paramref name="startsAtPageEndpoint"/>). A later burst keeps the bound anonymous actor of
    /// a run admitted that way.
    /// </summary>
    public static string? AnonymousRefusal(CallerContext caller, Workflow workflow, bool firstBurst, bool startsAtPageEndpoint)
    {
        if (caller.IsAuthenticated || caller.IsSystem)
        {
            return null;
        }

        var name = workflow.WorkflowMetadata.Name ?? workflow.Identity.DefinitionId;
        if (!AcceptsAnonymousRuns(workflow))
        {
            return $"Workflow '{name}' does not accept anonymous runs: sign in, or have it published to accept anonymous runs from a public page.";
        }

        return firstBurst && !startsAtPageEndpoint
            ? $"Workflow '{name}' accepts anonymous runs only from a page-URL HTTP endpoint; this run was started some other way."
            : null;
    }

    /// <summary>
    /// The caller a burst of <paramref name="workflow"/> with <paramref name="input"/> acts as;
    /// throws <see cref="WorkflowAccessRefusedException"/> when there is none. The actor is read
    /// only through the seal: bound to <paramref name="instanceId"/>, or fresh on the run's
    /// first burst (<paramref name="firstBurst"/>).
    /// </summary>
    public async Task<CallerContext> ForRunAsync(Workflow workflow, IDictionary<string, object>? input, string? instanceId, bool firstBurst, CancellationToken cancellationToken = default)
    {
        if (RunsAsSystem(workflow))
        {
            return await factory.CreateSystemAsync(cancellationToken: cancellationToken);
        }

        var actor = input is not null && input.TryGetValue(WorkflowsConstants.InputKeys.Actor, out var value) ? seal.Open(value, instanceId, firstBurst) : null;
        if (actor is null)
        {
            throw new WorkflowAccessRefusedException($"Workflow '{workflow.WorkflowMetadata.Name ?? workflow.Identity.DefinitionId}' has no actor to run as: it was started without one (a timer, a cron or a delay), or with one the server did not seal for this run, and is not published as system. Publish it as system, or start it from a request or a trigger.");
        }

        return await FromActorAsync(actor, cancellationToken);
    }

    /// <summary>
    /// The caller of the burst that will resume <paramref name="workflowInstanceId"/>: the
    /// definition's system flag, else the actor bound to the instance. Null when the instance
    /// is unknown (the engine then refuses the resume itself).
    /// </summary>
    public async Task<CallerContext?> ForInstanceAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await instanceStore.FindAsync(new WorkflowInstanceFilter { Id = workflowInstanceId }, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        var graph = await definitionService.FindWorkflowGraphAsync(instance.DefinitionVersionId, cancellationToken);
        return graph is null ? null : await ForRunAsync(graph.Workflow, instance.WorkflowState.Input, instance.Id, firstBurst: false, cancellationToken);
    }

    /// <summary>The caller of a new run of <paramref name="definitionHandle"/> with <paramref name="input"/>; null when the definition is unknown.</summary>
    public async Task<CallerContext?> ForDefinitionAsync(WorkflowDefinitionHandle definitionHandle, IDictionary<string, object>? input, CancellationToken cancellationToken = default)
    {
        var graph = await definitionService.FindWorkflowGraphAsync(definitionHandle, cancellationToken);
        return graph is null ? null : await ForRunAsync(graph.Workflow, input, instanceId: null, firstBurst: true, cancellationToken);
    }

    /// <summary>
    /// The actor as a caller with current rights. A system actor is refused here: the system
    /// caller is produced only for a definition published as system, never from an input
    /// (docs/operations.md › Decisions › System actors).
    /// </summary>
    public Task<CallerContext> FromActorAsync(WorkflowUserContext actor, CancellationToken cancellationToken = default)
    {
        if (actor.IsSystem)
        {
            throw new WorkflowAccessRefusedException("The run was captured while the system was acting; a workflow acts as the system only when its definition is published as system.");
        }

        return factory.CreateAsync(new CallerRequest(actor.ToIdentityPrincipal(), actor.Side, actor.OrganizationId, null), cancellationToken);
    }

    /// <summary>The caller the request path's gate set for this request, if any.</summary>
    public CallerContext? Current => accessor.Current;

    /// <summary>
    /// The caller of the current request: the one the gate built. Null when no gate ran; the
    /// principal is never read here (docs/access.md › One place reads the principal).
    /// </summary>
    public Task<CallerContext?> ForRequestAsync(CallerSide side, CancellationToken cancellationToken = default) =>
        Task.FromResult(accessor.Current);
}
