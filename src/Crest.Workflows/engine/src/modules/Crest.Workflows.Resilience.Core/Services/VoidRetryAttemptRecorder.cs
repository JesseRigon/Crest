using Crest.Workflows.Resilience.Models;

namespace Crest.Workflows.Resilience;

public class VoidRetryAttemptRecorder : IRetryAttemptRecorder
{
    public static VoidRetryAttemptRecorder Instance { get; } = new();
    
    public Task RecordAsync(RecordRetryAttemptsContext context)
    {
        // Send records into the void.
        return Task.CompletedTask;
    }
}