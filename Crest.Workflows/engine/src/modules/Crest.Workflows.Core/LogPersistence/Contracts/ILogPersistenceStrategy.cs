namespace Crest.Workflows.LogPersistence;

public interface ILogPersistenceStrategy
{
    Task<LogPersistenceMode> GetPersistenceModeAsync(LogPersistenceStrategyContext context);
}