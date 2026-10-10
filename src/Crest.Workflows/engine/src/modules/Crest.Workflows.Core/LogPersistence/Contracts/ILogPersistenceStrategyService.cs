namespace Crest.Workflows.LogPersistence;

public interface ILogPersistenceStrategyService
{
    IEnumerable<ILogPersistenceStrategy> ListStrategies();
}