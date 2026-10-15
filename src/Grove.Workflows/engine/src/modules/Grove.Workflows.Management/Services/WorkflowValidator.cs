using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Activities;
using Grove.Workflows.Management.Models;
using Grove.Workflows.Management.Notifications;

namespace Grove.Workflows.Management.Services;

/// <inheritdoc />
public class WorkflowValidator(INotificationSender notificationSender) : IWorkflowValidator
{
    /// <inheritdoc />
    public async Task<IEnumerable<WorkflowValidationError>> ValidateAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var validationErrors = new List<WorkflowValidationError>();
        var notification = new WorkflowDefinitionValidating(workflow, validationErrors);
        await notificationSender.SendAsync(notification, cancellationToken);
        return validationErrors;
    }
}