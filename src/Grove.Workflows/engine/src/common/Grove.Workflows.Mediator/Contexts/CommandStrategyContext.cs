using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Middleware.Command;

namespace Grove.Workflows.Mediator.Contexts;

/// <summary>
/// Represents a context for executing a command.
/// </summary>
/// <param name="CommandContext">The command context to execute.</param>
/// <param name="Handler">The command handler.</param>
/// <param name="ServiceProvider">The service provider to resolve services from.</param>
/// <param name="CancellationToken">The cancellation token.</param>
public record CommandStrategyContext(CommandContext CommandContext, ICommandHandler Handler, IServiceProvider ServiceProvider, CancellationToken CancellationToken = default);