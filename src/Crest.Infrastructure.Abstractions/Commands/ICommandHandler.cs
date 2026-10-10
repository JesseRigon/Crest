namespace Crest.Environment.Commands;

public interface ICommandHandler
{
    Task ExecuteAsync(CommandContext context);
}
