using Grove.Environment.Shell.Builders;

namespace Grove.Abstractions.Shell;

public interface IShellContextEvents
{
    Task CreatedAsync(ShellContext context);
}
