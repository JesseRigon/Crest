namespace Grove.Workflows.LogPersistence;

public interface ILogPersistenceStrategyService
{
    IEnumerable<ILogPersistenceStrategy> ListStrategies();
}