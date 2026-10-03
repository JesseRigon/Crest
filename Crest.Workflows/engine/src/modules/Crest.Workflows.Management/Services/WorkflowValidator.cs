using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Activities;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Notifications;

namespace Crest.Workflows.Management.Services;

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