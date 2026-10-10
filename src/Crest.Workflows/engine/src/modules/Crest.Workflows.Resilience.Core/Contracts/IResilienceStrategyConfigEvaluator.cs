using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Resilience.Models;

namespace Crest.Workflows.Resilience;

public interface IResilienceStrategyConfigEvaluator
{
    Task<IResilienceStrategy?> EvaluateAsync(ResilienceStrategyConfig? config, ExpressionExecutionContext context, CancellationToken cancellationToken = default);
}