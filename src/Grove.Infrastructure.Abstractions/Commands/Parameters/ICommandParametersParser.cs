namespace Grove.Environment.Commands.Parameters;

public interface ICommandParametersParser
{
    CommandParameters Parse(IEnumerable<string> args);
}
