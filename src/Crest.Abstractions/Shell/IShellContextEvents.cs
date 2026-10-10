using Crest.Environment.Shell.Builders;

namespace Crest.Abstractions.Shell;

public interface IShellContextEvents
{
    Task CreatedAsync(ShellContext context);
}
