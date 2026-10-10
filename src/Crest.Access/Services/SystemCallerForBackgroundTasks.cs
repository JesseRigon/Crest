using Crest.BackgroundTasks;

namespace Crest.Access.Services;

/// <summary>Scheduled background tasks run as the tenant system actor, whatever an earlier
/// step of the same scope may have set (the tenant pipeline run for a background task does
/// not gate, but if it did the task must not inherit an anonymous caller).</summary>
public sealed class SystemCallerForBackgroundTasks(ICallerContextAccessor accessor, ICallerContextFactory factory) : IBackgroundTaskEventHandler
{
    public async Task ExecutingAsync(BackgroundTaskEventContext context, CancellationToken cancellationToken)
    {
        accessor.Current = await factory.CreateSystemAsync(cancellationToken: cancellationToken);
    }

    public Task ExecutedAsync(BackgroundTaskEventContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
