using Grove.Workflows.Resilience.Models;
using Grove.Workflows;

namespace Grove.Workflows.Resilience;

public interface IResilientActivity : IActivity
{
    IDictionary<string, string?> CollectRetryDetails(ActivityExecutionContext context, RetryAttempt attempt);
}