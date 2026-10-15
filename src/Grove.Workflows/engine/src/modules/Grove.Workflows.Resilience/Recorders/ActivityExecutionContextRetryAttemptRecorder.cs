using Grove.Workflows.Resilience.Models;
using JetBrains.Annotations;

namespace Grove.Workflows.Resilience.Recorders;

[UsedImplicitly]
public class ActivityExecutionContextRetryAttemptRecorder : IRetryAttemptRecorder
{
    public Task RecordAsync(RecordRetryAttemptsContext context)
    {
        context.ActivityExecutionContext.Properties["RetryAttempts"] = context.Attempts;
        return Task.CompletedTask;
    }
}