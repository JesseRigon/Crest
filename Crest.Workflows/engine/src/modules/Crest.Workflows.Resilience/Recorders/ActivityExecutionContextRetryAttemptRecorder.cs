using Crest.Workflows.Resilience.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Resilience.Recorders;

[UsedImplicitly]
public class ActivityExecutionContextRetryAttemptRecorder : IRetryAttemptRecorder
{
    public Task RecordAsync(RecordRetryAttemptsContext context)
    {
        context.ActivityExecutionContext.Properties["RetryAttempts"] = context.Attempts;
        return Task.CompletedTask;
    }
}