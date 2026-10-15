namespace Grove.BackgroundTasks;

public interface IBackgroundTask
{
    Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
