using Cronos;
using Crest.Workflows.Scheduling.Bookmarks;
using Crest.Workflows.Activities;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Runtime.Contracts;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Scheduling.TriggerPayloadValidators;

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