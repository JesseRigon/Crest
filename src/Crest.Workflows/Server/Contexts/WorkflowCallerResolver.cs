using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crest.Access;
using Crest.Workflows.Activities;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Models;
using Crest.Workflows.Registry;
using Microsoft.AspNetCore.Http;

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
    IHttpContextAccessor httpContextAccessor,
    IWorkflowInstanceStore instanceStore,
    IWorkflowDefinitionService definitionService)
{
    /// <summary>Whether the definition is published as system (<see cref="WorkflowsConstants.RunsAsSystemProperty"/>).</summary>
    public static bool RunsAsSystem(Workflow workflow) => RunsAsSystem(workflow.CustomProperties);

    public static bool RunsAsSystem(IDictionary<string, object>? properties) =>
        properties is not null && bool.TryParse(WorkflowOwnershipInfo.Text(properties, WorkflowsConstants.RunsAsSystemProperty), out var runsAsSystem) && runsAsSystem;

    /// <summary>The caller a burst of <paramref name="workflow"/> with <paramref name="input"/> acts as; throws <see cref="WorkflowAccessRefusedException"/> when there is none.</summary>
    public async Task<CallerContext> ForRunAsync(Workflow workflow, IDictionary<string, object>? input, CancellationToken cancellationToken = default)
    {
        if (RunsAsSystem(workflow))
        {
            return await factory.CreateSystemAsync(cancellationToken: cancellationToken);
        }

        var actor = input is not null && input.TryGetValue(WorkflowsConstants.InputKeys.Actor, out var value) ? Actor(value) : null;
        if (actor is null)
        {
            throw new WorkflowAccessRefusedException($"Workflow '{workflow.WorkflowMetadata.Name ?? workflow.Identity.DefinitionId}' has no actor to run as: it was started without one (a timer, a cron or a delay) and is not published as system. Publish it as system, or start it from a request or a trigger.");
        }

        return await FromActorAsync(actor, cancellationToken);
    }

    /// <summary>
    /// The caller of the burst that will resume <paramref name="workflowInstanceId"/>: the
    /// definition's system flag, else the actor persisted in the instance input. Null when the
    /// instance is unknown (the engine then refuses the resume itself).
    /// </summary>
    public async Task<CallerContext?> ForInstanceAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await instanceStore.FindAsync(new WorkflowInstanceFilter { Id = workflowInstanceId }, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        var graph = await definitionService.FindWorkflowGraphAsync(instance.DefinitionVersionId, cancellationToken);
        return graph is null ? null : await ForRunAsync(graph.Workflow, instance.WorkflowState.Input, cancellationToken);
    }

    /// <summary>The caller of a new run of <paramref name="definitionHandle"/> with <paramref name="input"/>; null when the definition is unknown.</summary>
    public async Task<CallerContext?> ForDefinitionAsync(WorkflowDefinitionHandle definitionHandle, IDictionary<string, object>? input, CancellationToken cancellationToken = default)
    {
        var graph = await definitionService.FindWorkflowGraphAsync(definitionHandle, cancellationToken);
        return graph is null ? null : await ForRunAsync(graph.Workflow, input, cancellationToken);
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

    /// <summary>The caller of the current request: the gated one, else built from the request's principal on <paramref name="side"/>; null with no request.</summary>
    public async Task<CallerContext?> ForRequestAsync(CallerSide side, CancellationToken cancellationToken = default)
    {
        if (accessor.Current is { } current)
        {
            return current;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        return await factory.CreateAsync(new CallerRequest(httpContext.User, side, null, null), cancellationToken);
    }

    /// <summary>The actor stored in a workflow input: the object as captured in this process, or its persisted JSON.</summary>
    public static WorkflowUserContext? Actor(object? value) => value switch
    {
        null => null,
        WorkflowUserContext actor => actor,
        JsonElement element when element.ValueKind == JsonValueKind.Object => element.Deserialize<WorkflowUserContext>(ActorJson),
        JsonObject node => node.Deserialize<WorkflowUserContext>(ActorJson),
        _ => null,
    };

    private static readonly JsonSerializerOptions ActorJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
