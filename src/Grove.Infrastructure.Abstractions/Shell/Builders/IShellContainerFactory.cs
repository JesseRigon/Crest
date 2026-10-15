using Grove.Environment.Shell.Builders.Models;

namespace Grove.Environment.Shell.Builders;

public interface IShellContainerFactory
{
    Task<IServiceProvider> CreateContainerAsync(ShellSettings settings, ShellBlueprint blueprint);
}
