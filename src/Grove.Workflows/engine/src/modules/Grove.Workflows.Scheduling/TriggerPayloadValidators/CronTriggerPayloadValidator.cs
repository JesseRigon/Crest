using Cronos;
using Grove.Workflows.Scheduling.Bookmarks;
using Grove.Workflows.Activities;
using Grove.Workflows.Management.Models;
using Grove.Workflows.Runtime.Contracts;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Scheduling.TriggerPayloadValidators;

public class CronTriggerPayloadValidator(ICronParser cronParser) : ITriggerPayloadValidator<CronTriggerPayload>
{
    public Task ValidateAsync(
        CronTriggerPayload payload,
        Workflow workflow,
        StoredTrigger trigger,
        ICollection<WorkflowValidationError> validationErrors,
        CancellationToken cancellationToken)
    {
        try
        {
            cronParser.GetNextOccurrence(payload.CronExpression);
        }
        catch (CronFormatException ex)
        {
            validationErrors.Add(new("Error when parsing cron expression: " + ex.Message,
                trigger.ActivityId));
        }
        return Task.CompletedTask;
    }
}