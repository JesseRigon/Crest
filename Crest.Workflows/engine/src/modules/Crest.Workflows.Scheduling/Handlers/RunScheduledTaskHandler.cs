using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;
using Crest.Workflows.Scheduling.Commands;

namespace Crest.Workflows.Scheduling.Handlers;

/// <summary>
/// A command handler for <see cref="RunScheduledTask"/>.
/// </summary>
public class RunScheduledTaskHandler : ICommandHandler<RunScheduledTask>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunScheduledTaskHandler"/> class.
    /// </summary>
    public RunScheduledTaskHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task<Unit> HandleAsync(RunScheduledTask command, CancellationToken cancellationToken)
    {
        var taskExecutionContext = new TaskExecutionContext(_serviceProvider, cancellationToken);
        await command.Task.ExecuteAsync(taskExecutionContext);
        return Unit.Instance;
    }
}