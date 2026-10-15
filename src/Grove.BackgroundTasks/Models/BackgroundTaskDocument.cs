using Grove.Data.Documents;

namespace Grove.BackgroundTasks.Models;

public class BackgroundTaskDocument : Document
{
    public Dictionary<string, BackgroundTaskSettings> Settings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
