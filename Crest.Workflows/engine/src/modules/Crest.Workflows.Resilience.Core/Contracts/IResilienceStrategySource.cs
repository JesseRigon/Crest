namespace Crest.Workflows.Resilience;

public interface IResilienceStrategySource
{
    Task<IEnumerable<IResilienceStrategy>> GetStrategiesAsync(CancellationToken cancellationToken = default);
}