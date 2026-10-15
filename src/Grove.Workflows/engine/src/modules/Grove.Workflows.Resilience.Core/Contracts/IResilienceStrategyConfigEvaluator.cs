using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Resilience.Models;

namespace Grove.Workflows.Resilience;

public interface IResilienceStrategyConfigEvaluator
{
    Task<IResilienceStrategy?> EvaluateAsync(ResilienceStrategyConfig? config, ExpressionExecutionContext context, CancellationToken cancellationToken = default);
}