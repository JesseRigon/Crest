using Crest.Workflows.Mediator.Abstractions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Middleware.Command;

namespace Crest.Workflows.Mediator.Channels;

/// <inheritdoc cref="Crest.Workflows.Mediator.Contracts.ICommandsChannel" />
public class CommandsChannel : ChannelBase<CommandContext>, ICommandsChannel
{
}