using System.Reflection;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Activities;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Management.Notifications;
using Crest.Workflows.Runtime.Contracts;
using Crest.Workflows.Runtime.Entities;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Runtime.Handlers;

/// <summary>
/// A <see cref="WorkflowDefinitionValidating"/> handler that validates a workflow path and return any errors.
/// </summary>
[UsedImplicitly]
public class ValidateWorkflowRequestHandler(
    ITriggerIndexer triggerIndexer,
    IServiceProvider serviceProvider) : INotificationHandler<WorkflowDefinitionValidating>
{
    public async Task HandleAsync(WorkflowDefinitionValidating notification, CancellationToken cancellationToken)
    {
        Workflow workflow = notification.Workflow;
        ICollection<WorkflowValidationError> validationErrors = notification.ValidationErrors;
        foreach (StoredTrigger trigger in await triggerIndexer.GetTriggersAsync(workflow, cancellationToken))
        {
            if (trigger.Payload is null)
            {
                validationErrors.Add(new($"Trigger should have a payload",
                    trigger.ActivityId));
            }
            else
            {
                MethodInfo method = typeof(ValidateWorkflowRequestHandler)
                    .GetMethod(nameof(ValidateInternalAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
                    .MakeGenericMethod(trigger.Payload.GetType());

                object? result = method.Invoke(this, [trigger.Payload, workflow, trigger, validationErrors, cancellationToken]);
                if (result is Task task)
                {
                    await task;
                }
            }
        }
    }

    private async Task ValidateInternalAsync<TPayload>(TPayload instance,
        Workflow workflow,
        StoredTrigger trigger,
        ICollection<WorkflowValidationError> validationErrors,
        CancellationToken cancellationToken)
    {
        ITriggerPayloadValidator<TPayload>? payloadValidator = serviceProvider.GetService<ITriggerPayloadValidator<TPayload>>();

        if (payloadValidator != null)
            await payloadValidator.ValidateAsync(instance, workflow, trigger, validationErrors, cancellationToken);
    }
}