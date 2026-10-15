using Grove.Workflows.Mediator.Contracts;

namespace Grove.Workflows.Scheduling.Commands;

/// <summary>
/// A command to run a scheduled task.
/// </summary>
public record RunScheduledTask(ITask Task) : ICommand;