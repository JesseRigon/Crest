namespace Grove.Workflows.Common;

public interface ITask
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}