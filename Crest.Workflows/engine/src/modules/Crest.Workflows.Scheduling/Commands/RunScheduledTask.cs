using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Scheduling.Commands;

/// <summary>
/// A command to run a scheduled task.
/// </summary>
public record RunScheduledTask(ITask Task) : ICommand;