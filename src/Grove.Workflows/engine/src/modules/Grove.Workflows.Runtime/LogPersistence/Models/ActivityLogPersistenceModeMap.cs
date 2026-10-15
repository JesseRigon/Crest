using Grove.Workflows.LogPersistence;

namespace Grove.Workflows.Runtime;

public class ActivityLogPersistenceModeMap
{
    public IDictionary<string, LogPersistenceMode> Inputs { get; set; } = new Dictionary<string, LogPersistenceMode>();
    public IDictionary<string, LogPersistenceMode> Outputs { get; set; } = new Dictionary<string, LogPersistenceMode>();
    public LogPersistenceMode InternalState { get; set; }
}