using System.Threading.Channels;
using Grove.Workflows.Mediator.Middleware.Command;

namespace Grove.Workflows.Mediator.Contracts;

/// <summary>
/// A channel that can be used to enqueue commands.
/// </summary>
public interface ICommandsChannel
{
    /// <summary>
    /// Gets the writer for the commands queue.
    /// </summary>
    ChannelWriter<CommandContext> Writer { get; }
    
    /// <summary>
    /// Gets the reader for the commands queue.
    /// </summary>
    ChannelReader<CommandContext> Reader { get; }
}