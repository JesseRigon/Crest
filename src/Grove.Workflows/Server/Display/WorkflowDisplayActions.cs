using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Display.Management.Components;
using Grove.Workflows.Contexts;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Security;

namespace Grove.Workflows.Display;

/// <summary>
/// The one decision for a display tree's workflow action (docs/blazor-display.md § 4a): the
/// <see cref="WorkflowsConstants.Permissions.Run"/> decision on the definition, with its Run
/// list as the veto beside it, as an HTTP endpoint workflow is decided. The tree filter asks it
/// when the tree is built (an item the viewer cannot run is left out); the run endpoint asks it
/// again when the action fires.
/// </summary>
public sealed class WorkflowDisplayActionAuthorizer(IAccessDecision decision, IWorkflowDefinitionAccessReader access)
{
    public async Task<bool> AllowsAsync(CallerContext caller, string definitionId, CancellationToken cancellationToken = default)
    {
        var resource = await access.GetAsync(definitionId) ?? new WorkflowDefinitionAccessResource(definitionId, [], []);
        var verdict = await decision.DecideAsync(caller, WorkflowsConstants.Permissions.Run, resource, cancellationToken);
        return verdict.IsAllowed && resource.Admits(caller, WorkflowsConstants.Permissions.Run);
    }
}

/// <summary>
/// The UI action contract of a workflow: the inputs its <see cref="UiActionTrigger"/> declares, read
/// from the published version's stored trigger; null when the workflow is not runnable from the UI.
/// </summary>
public interface IUiActionContracts
{
    Task<UiActionContract?> FindAsync(string definitionId, CancellationToken cancellationToken = default);
}

public sealed class UiActionContracts(ITriggerStore triggers) : IUiActionContracts
{
    private static readonly string s_triggerName = Grove.Workflows.Helpers.ActivityTypeNameHelper.GenerateTypeName<UiActionTrigger>();

    public async Task<UiActionContract?> FindAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        var stored = (await triggers.FindManyAsync(new TriggerFilter { WorkflowDefinitionId = definitionId, Name = s_triggerName }, cancellationToken)).FirstOrDefault();
        if (stored is null)
        {
            return null;
        }

        // The payload comes back as the record or as JSON, depending on the store.
        var declaration = stored.Payload switch
        {
            UiActionTriggerPayload payload => payload.Inputs,
            null => null,
            var other => JsonSerializer.SerializeToNode(other) is JsonObject json
                ? (json["Inputs"] ?? json["inputs"])?.GetValue<string>()
                : null,
        };

        return UiActionContract.Parse(declaration);
    }
}

/// <summary>Answers for workflow actions in the display tree filter: the Run decision, and the workflow must offer a UI action contract.</summary>
public sealed class WorkflowDisplayActionFilter(WorkflowDisplayActionAuthorizer authorizer, IUiActionContracts contracts) : IDisplayActionFilter
{
    public async Task<bool?> AllowsAsync(CallerContext caller, DisplayAction action, CancellationToken cancellationToken = default) =>
        action.Kind != DisplayActionKind.Workflow ? null
        : string.IsNullOrWhiteSpace(action.Workflow) ? false
        : await contracts.FindAsync(action.Workflow, cancellationToken) is not null
            && await authorizer.AllowsAsync(caller, action.Workflow, cancellationToken);
}

/// <summary>Checks a template's workflow action on save: the workflow offers a UI action, and the action passes exactly its inputs.</summary>
public sealed class WorkflowDisplayActionValidator(IUiActionContracts contracts) : IDisplayActionValidator
{
    public async Task<IReadOnlyList<string>> ValidateAsync(DisplayAction action, CancellationToken cancellationToken = default)
    {
        if (action.Kind != DisplayActionKind.Workflow || string.IsNullOrWhiteSpace(action.Workflow))
        {
            return [];
        }

        if (await contracts.FindAsync(action.Workflow, cancellationToken) is not { } contract)
        {
            return [$"The workflow '{action.Workflow}' is not published with a UI action trigger."];
        }

        return contract.CheckNames(action.Arguments?.Keys ?? Enumerable.Empty<string>());
    }
}

/// <summary>A run's answer; a refusal of the arguments carries its <paramref name="Errors"/> by input name.</summary>
public sealed record WorkflowActionResult(string Status, JsonNode? Output, string? Error, IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors = null);
