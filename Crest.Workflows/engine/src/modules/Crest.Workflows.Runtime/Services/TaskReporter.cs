using Crest.Workflows.Helpers;
using Crest.Workflows.Runtime.Activities;
using Crest.Workflows.Runtime.Options;
using Crest.Workflows.Runtime.Stimuli;

namespace Crest.Workflows.Runtime;

/// <inheritdoc />
public class TaskReporter(IBookmarkQueue bookmarkQueue, IStimulusHasher stimulusHasher) : ITaskReporter
{
    private static readonly string ActivityTypeName = ActivityTypeNameHelper.GenerateTypeName<RunTask>();
    
    /// <inheritdoc />
    public async Task ReportCompletionAsync(string taskId, object? result = null, CancellationToken cancellationToken = default)
    {
        var stimulus = new RunTaskStimulus(taskId, null!);

        var input = new Dictionary<string, object>
        {
            [RunTask.InputKey] = result!
        };

        var bookmarkQueueItem = new NewBookmarkQueueItem
        {
            ActivityTypeName = ActivityTypeName,
            StimulusHash = stimulusHasher.Hash(ActivityTypeName, stimulus),
            Options = new()
            {
                Input = input
            }
        };
        
        await bookmarkQueue.EnqueueAsync(bookmarkQueueItem, cancellationToken);
    }
}