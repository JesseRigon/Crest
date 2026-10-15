using Grove.Workflows.Mediator.Abstractions;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Middleware.Command;

namespace Grove.Workflows.Mediator.Channels;

/// <inheritdoc cref="Grove.Workflows.Mediator.Contracts.ICommandsChannel" />
public class CommandsChannel : ChannelBase<CommandContext>, ICommandsChannel
{
}