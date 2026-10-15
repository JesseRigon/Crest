using Microsoft.Extensions.Primitives;

namespace Grove.BackgroundTasks;

public interface IBackgroundTaskSettingsProvider
{
    IChangeToken ChangeToken { get; }
    Task<BackgroundTaskSettings> GetSettingsAsync(IBackgroundTask task);
}
