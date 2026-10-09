using Microsoft.Extensions.Primitives;
using Crest.Environment.Cache;

namespace Crest.BackgroundTasks.Services;

public class BackgroundTaskSettingsProvider : IBackgroundTaskSettingsProvider
{
    private readonly BackgroundTaskManager _backgroundTaskManager;
    private readonly ISignal _signal;

    public BackgroundTaskSettingsProvider(BackgroundTaskManager backgroundTaskManager, ISignal signal)
    {
        _backgroundTaskManager = backgroundTaskManager;
        _signal = signal;
    }

    public IChangeToken ChangeToken => _signal.GetToken(nameof(BackgroundTaskSettings));

    public async Task<BackgroundTaskSettings> GetSettingsAsync(IBackgroundTask task)
    {
        var document = await _backgroundTaskManager.GetDocumentAsync();

        if (document.Settings.TryGetValue(task.GetTaskName(), out var settings))
        {
            return settings;
        }

        return null;
    }
}
