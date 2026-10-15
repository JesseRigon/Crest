using Grove.Workflows.Management.Models;
using Grove.Workflows.Management.Notifications;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Units;

namespace Grove.Workflows.Hooks;

/// <summary>
/// Publishing a definition that hook attachments use re-derives its atomicity: a version
/// that would stop being atomic (a wait or an external call added) is refused, with the
/// boundary named, because it would make the host's transaction impossible to keep. Hooked
/// into the engine's validation through its notification.
/// </summary>
public sealed class HookAttachmentAtomicityValidator(WorkflowHookService hooks, WorkflowAtomicityAnalyzer analyzer) : INotificationHandler<WorkflowDefinitionValidating>
{
    public async Task HandleAsync(WorkflowDefinitionValidating notification, CancellationToken cancellationToken)
    {
        var definitionId = notification.Workflow.Identity.DefinitionId;
        if (string.IsNullOrEmpty(definitionId) || (await hooks.AttachmentsOfAsync(definitionId)).Count == 0)
        {
            return;
        }

        // The version being published, not the stored one.
        var report = await analyzer.AnalyzeAsync(notification.Workflow, cancellationToken);
        if (report.IsAtomic)
        {
            return;
        }

        notification.ValidationErrors.Add(new WorkflowValidationError($"This flow is attached to a hook and must stay atomic (one transaction, no waits, no external calls): {report.Describe()}. Detach it from the hook or move those steps to a flow on the event side.", report.Boundaries[0].ActivityId));
    }
}
