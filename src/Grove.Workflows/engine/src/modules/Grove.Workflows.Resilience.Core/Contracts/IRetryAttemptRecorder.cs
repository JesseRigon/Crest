using Grove.Workflows.Resilience.Models;

namespace Grove.Workflows.Resilience;

public interface IRetryAttemptRecorder
{
    Task RecordAsync(RecordRetryAttemptsContext context);
}