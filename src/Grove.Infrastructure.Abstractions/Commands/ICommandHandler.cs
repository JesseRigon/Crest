namespace Grove.Environment.Commands;

public interface ICommandHandler
{
    Task ExecuteAsync(CommandContext context);
}
