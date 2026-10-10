using Crest.Data.Documents;

namespace Crest.BackgroundTasks.Models;

public class BackgroundTaskDocument : Document
{
    public Dictionary<string, BackgroundTaskSettings> Settings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
