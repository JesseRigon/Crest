using Crest.Workflows.Resilience.Models;

namespace Crest.Workflows.Resilience;

public interface IRetryAttemptRecorder
{
    Task RecordAsync(RecordRetryAttemptsContext context);
}