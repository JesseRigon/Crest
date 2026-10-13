using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.DisplayManagement.Components;
using Crest.Workflows.Contexts;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.Messages;
using Crest.Workflows.Security;
using Crest.Workflows.Units;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Workflows.Display;

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
    private static readonly string s_triggerName = Crest.Workflows.Helpers.ActivityTypeNameHelper.GenerateTypeName<UiActionTrigger>();

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

/// <summary>
/// Runs a display action's workflow for the request's caller (§ 4a): only a workflow whose
/// published version starts with a <see cref="UiActionTrigger"/>, its arguments checked against
/// the trigger's declared inputs; the published definition,
/// one unit of work, the caller sealed into the run as its actor, the input as the run's
/// payload. Antiforgery-protected: an action fires on the viewer's interaction only (security
/// audit item 8). Answers the run's status and output; a run that suspends answers its status
/// and the client treats the action as done.
/// </summary>
[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/workflows/actions")]
public sealed class WorkflowActionsController(
    WorkflowCallerResolver callers,
    WorkflowDisplayActionAuthorizer authorizer,
    IUiActionContracts contracts,
    IWorkflowRuntime runtime,
    WorkflowUnitOfWork unit,
    WorkflowActorSeal seal) : ControllerBase
{
    [HttpPost("{definitionId}")]
    public async Task<ActionResult<WorkflowActionResult>> RunAsync(string definitionId, [FromBody] JsonObject? input, CancellationToken cancellationToken)
    {
        var caller = await callers.ForRequestAsync(CallerSide.Site, cancellationToken);
        if (caller is null || !await authorizer.AllowsAsync(caller, definitionId, cancellationToken))
        {
            // The same answer whether the flow is missing or refused.
            return NotFound();
        }

        // Only a workflow that offers a UI action is runnable from the UI, with exactly its inputs.
        if (await contracts.FindAsync(definitionId, cancellationToken) is not { } contract)
        {
            return NotFound();
        }

        var problems = contract.CheckByInput(input ?? []);
        if (problems.Count > 0)
        {
            return BadRequest(new WorkflowActionResult("Invalid", null, string.Join(" ", problems.Values.SelectMany(errors => errors)), problems));
        }

        var payload = input is null
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(input.ToJsonString()) ?? [];

        RunWorkflowInstanceResponse response;
        string? failure;
        using (var frame = unit.Enter())
        {
            var client = await runtime.CreateClientAsync(cancellationToken);
            try
            {
                response = await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
                {
                    WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(definitionId, VersionOptions.Published),
                    Input = new Dictionary<string, object>
                    {
                        [WorkflowsConstants.InputKeys.Payload] = payload,
                        [WorkflowsConstants.InputKeys.Actor] = seal.Seal(WorkflowUserContext.From(caller)),
                    },
                }, cancellationToken);
            }
            catch (Crest.Workflows.Runtime.Exceptions.WorkflowGraphNotFoundException)
            {
                // No published version: the same answer as a refusal.
                return NotFound();
            }

            failure = frame.IsFailed ? frame.Reason
                : response.SubStatus == WorkflowSubStatus.Faulted ? response.Incidents.FirstOrDefault()?.Message ?? "faulted"
                : null;
        }

        if (failure is not null)
        {
            return Conflict(new WorkflowActionResult(response.Status.ToString(), null, failure));
        }

        var output = response.Output is { Count: > 0 } values ? JsonSerializer.SerializeToNode(values) : null;
        return Ok(new WorkflowActionResult(response.Status.ToString(), output, null));
    }
}

/// <summary>A run's answer; a refusal of the arguments carries its <paramref name="Errors"/> by input name.</summary>
public sealed record WorkflowActionResult(string Status, JsonNode? Output, string? Error, IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors = null);
