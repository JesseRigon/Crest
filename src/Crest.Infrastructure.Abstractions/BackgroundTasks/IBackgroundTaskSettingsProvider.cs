using Microsoft.Extensions.Primitives;

namespace Crest.BackgroundTasks;

public interface IBackgroundTaskSettingsProvider
{
    IChangeToken ChangeToken { get; }
    Task<BackgroundTaskSettings> GetSettingsAsync(IBackgroundTask task);
}
