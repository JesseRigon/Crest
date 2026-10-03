using Crest.Workflows.Common.Models;
using Crest.Workflows.Contexts;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Messages;
using Crest.Workflows.Units;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Hooks;

/// <summary>
/// Runs a slot's attachments inline, inside the current unit of work (plans/workflows.md ›
/// Posting on workflows): each attachment starts as a child instance in the same scope and
/// session. Used by the <c>Hook</c> activity from inside a flow and by registry services
/// from inside their own requests (<see cref="IWorkflowHookRunner"/>), so a tenant's
/// attachment to <c>transaction.created</c> runs whether the invoice came from a flow or
/// from the API. A required attachment that faults or suspends fails the unit and throws;
/// a best-effort one's failure is returned. Attachments are atomic by construction (checked
/// when attached and when published), so a hook never waits and never calls out.
/// </summary>
public sealed class WorkflowHookRunner(
    WorkflowHookService hooks,
    IWorkflowRuntime runtime,
    WorkflowUnitOfWork unit,
    IWorkflowUserContextAccessor userContext,
    ILogger<WorkflowHookRunner> logger) : IWorkflowHookRunner
{
    public Task<WorkflowHookRunResult> RunAsync(string slotKey, string? correlationId, IDictionary<string, object>? payload = null, CancellationToken cancellationToken = default)
    {
        var input = new Dictionary<string, object>
        {
            [WorkflowsConstants.InputKeys.Payload] = payload ?? new Dictionary<string, object>(),
            [WorkflowsConstants.InputKeys.Actor] = userContext.Capture(),
        };
        return RunAsync(slotKey, correlationId, input, parentInstanceId: null, failingActivityId: null, cancellationToken);
    }

    /// <summary>
    /// The full form: <paramref name="input"/> is the attachments' workflow input (the slot key
    /// is added), <paramref name="parentInstanceId"/> links the child instances to a hosting
    /// flow, <paramref name="failingActivityId"/> is the host node a failure is pinned on.
    /// </summary>
    public async Task<WorkflowHookRunResult> RunAsync(string slotKey, string? correlationId, IDictionary<string, object> input, string? parentInstanceId, string? failingActivityId, CancellationToken cancellationToken = default)
    {
        var slot = slotKey.Trim().ToLowerInvariant();
        var attachments = await hooks.ListAsync(slot, cancellationToken);
        var bestEffortFailures = new List<string>();
        var ran = 0;

        var childInput = new Dictionary<string, object>(input)
        {
            [WorkflowsConstants.InputKeys.HookSlot] = slot,
        };

        foreach (var attachment in attachments)
        {
            // The attachment's own frame: Fail unit inside it fails the frame, and this runner
            // decides what that means for the unit.
            string? failure;
            using (var frame = unit.Enter())
            {
                var client = await runtime.CreateClientAsync(cancellationToken);
                var response = await client.CreateAndRunInstanceAsync(new CreateAndRunWorkflowInstanceRequest
                {
                    WorkflowDefinitionHandle = WorkflowDefinitionHandle.ByDefinitionId(attachment.DefinitionId, VersionOptions.Published),
                    CorrelationId = correlationId,
                    ParentId = parentInstanceId,
                    Input = childInput,
                }, cancellationToken);
                ran++;

                failure = frame.IsFailed ? frame.Reason
                    : response.SubStatus == WorkflowSubStatus.Faulted ? response.Incidents.FirstOrDefault()?.Message ?? "faulted"
                    : response.Status != WorkflowStatus.Finished ? "the attachment suspended; a hook may not wait"
                    : null;
            }

            if (failure is null)
            {
                continue;
            }

            var name = attachment.DefinitionName ?? attachment.FlowKey ?? attachment.DefinitionId;
            if (attachment.Required)
            {
                var exception = new WorkflowHookFailedException(slot, name, failure);
                logger.LogWarning("{Message}", exception.Message);
                unit.Fail(exception.Message, failingActivityId);
                throw exception;
            }

            bestEffortFailures.Add($"{name}: {failure}");
        }

        return new(ran, bestEffortFailures);
    }
}
