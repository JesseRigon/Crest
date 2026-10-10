using Crest.Workflows.Resilience.Models;
using Crest.Workflows;

namespace Crest.Workflows.Resilience;

public interface IResilientActivity : IActivity
{
    IDictionary<string, string?> CollectRetryDetails(ActivityExecutionContext context, RetryAttempt attempt);
}