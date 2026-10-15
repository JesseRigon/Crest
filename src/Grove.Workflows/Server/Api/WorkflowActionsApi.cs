using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Api;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Contexts;
using Grove.Workflows.Display;
using Grove.Workflows.Models;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Messages;
using Grove.Workflows.Security;
using Grove.Workflows.Units;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/actions/{definitionId}</c>: runs a display action's workflow for the
/// request's caller (docs/blazor-display.md § 4a): only a workflow whose published version starts
/// with a <see cref="UiActionTrigger"/>, its arguments checked against the trigger's declared
/// inputs; the published definition, one unit of work, the caller sealed into the run as its
/// actor, the input as the run's payload. Antiforgery-protected (the group's filter): an action
/// fires on the viewer's interaction only (security audit item 8). Answers the run's status and
/// output; a run that suspends answers its status and the client treats the action as done.
/// </summary>
public sealed class WorkflowActionsApi : ApiContributor
{
    public override string Name => "WorkflowActions";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapPost("workflows/actions/{definitionId}", (HttpContext http, [FromRoute] string definitionId, [FromBody] JsonObject? input, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowActionsHandlers>(http).RunAsync(definitionId, input, cancellationToken));
}

public sealed class WorkflowActionsHandlers(
    WorkflowCallerResolver callers,
    WorkflowDisplayActionAuthorizer authorizer,
    IUiActionContracts contracts,
    IWorkflowRuntime runtime,
    WorkflowUnitOfWork unit,
    WorkflowActorSeal seal) : ApiHandlers
{
    public async Task<IResult> RunAsync(string definitionId, JsonObject? input, CancellationToken cancellationToken)
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
            catch (Grove.Workflows.Runtime.Exceptions.WorkflowGraphNotFoundException)
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
