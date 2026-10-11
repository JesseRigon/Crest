using Crest.Workflows.Common.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Activities.WorkflowDefinitionActivity;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Models;
using Crest.Workflows.Activities;

namespace Crest.Workflows.Units;

/// <summary>Where a flow stops being atomic: the node, its type, and why.</summary>
public sealed record WorkflowBoundary(string ActivityId, string ActivityType, string Reason, string? InDefinition = null);

public sealed record WorkflowAtomicityReport(bool IsAtomic, IReadOnlyList<WorkflowBoundary> Boundaries)
{
    public string Describe() => string.Join("; ", Boundaries.Select(b => $"{b.ActivityType.Split('.').Last()} '{b.ActivityId}'{(b.InDefinition is null ? string.Empty : $" (in {b.InDefinition})")}: {b.Reason}"));
}

/// <summary>
/// Derives, never asserts, whether a published definition is atomic: a single burst with no
/// waits and no external effects, nested published definitions walked. Boundaries are
/// triggers of any kind (waits, including a start trigger: an attachment is started by its
/// hook, not by an event), the engine's delay and HTTP request activities, anything marked
/// <see cref="IUnitBoundary"/>: connectors and the external stock tasks (engine background
/// activities, so a wait), approvals. Raise trigger is not one: the queue sends after commit
/// and the flow needs no answer.
/// </summary>
public sealed class WorkflowAtomicityAnalyzer(IWorkflowDefinitionService definitionService, IWorkflowGraphBuilder graphBuilder)
{
    // Engine activities by type name: the analyzer runs on materialized graphs, so a type
    // check would do, but the names keep this list readable and the HTTP module optional.
    private static readonly HashSet<string> BoundaryTypeNames = new(StringComparer.Ordinal)
    {
        "Crest.Workflows.Delay",
        "Crest.Workflows.SendHttpRequest",
        "Crest.Workflows.FlowSendHttpRequest",
        "Crest.Workflows.DispatchWorkflow",
        "Crest.Workflows.BulkDispatchWorkflows",
    };

    public async Task<WorkflowAtomicityReport?> AnalyzeAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var graph = await definitionService.FindWorkflowGraphAsync(definitionId, VersionOptions.Published, cancellationToken)
            ?? await definitionService.FindWorkflowGraphAsync(definitionId, VersionOptions.Latest, cancellationToken);
        if (graph is null)
        {
            return null;
        }

        return await AnalyzeAsync(graph, definitionId, cancellationToken);
    }

    /// <summary>The in-memory workflow about to be published.</summary>
    public async Task<WorkflowAtomicityReport> AnalyzeAsync(Workflow workflow, CancellationToken cancellationToken = default) =>
        await AnalyzeAsync(await graphBuilder.BuildAsync(workflow, cancellationToken), workflow.Identity.DefinitionId, cancellationToken);

    private async Task<WorkflowAtomicityReport> AnalyzeAsync(WorkflowGraph graph, string definitionId, CancellationToken cancellationToken)
    {
        var boundaries = new List<WorkflowBoundary>();
        await WalkAsync(graph, null, boundaries, new HashSet<string>(StringComparer.Ordinal) { definitionId }, cancellationToken);
        return new(boundaries.Count == 0, boundaries);
    }

    private async Task WalkAsync(WorkflowGraph graph, string? inDefinition, List<WorkflowBoundary> boundaries, HashSet<string> visited, CancellationToken cancellationToken)
    {
        foreach (var node in graph.Nodes)
        {
            var activity = node.Activity;
            if (activity is Workflow)
            {
                continue;
            }

            var reason = ReasonFor(activity);
            if (reason is not null)
            {
                boundaries.Add(new(activity.Id, activity.Type, reason, inDefinition));
                continue;
            }

            // A composable flow used as an activity: its own definition is walked.
            if (activity is WorkflowDefinitionActivity nested && !string.IsNullOrEmpty(nested.WorkflowDefinitionId) && visited.Add(nested.WorkflowDefinitionId))
            {
                var inner = await definitionService.FindWorkflowGraphAsync(nested.WorkflowDefinitionId, VersionOptions.Published, cancellationToken);
                if (inner is not null)
                {
                    await WalkAsync(inner, nested.WorkflowDefinitionId, boundaries, visited, cancellationToken);
                }
            }
        }
    }

    private static string? ReasonFor(IActivity activity)
    {
        if (activity is IUnitBoundary)
        {
            return "waits, or calls out of the database after the unit commits; put it on the event side";
        }

        if (activity is ITrigger)
        {
            return "a trigger waits for an event; a hook attachment is started by its hook";
        }

        if (BoundaryTypeNames.Contains(activity.Type))
        {
            return "waits or calls out of the database";
        }

        return null;
    }
}
