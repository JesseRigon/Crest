using Crest.Environment.Shell.Builders.Models;

namespace Crest.Environment.Shell.Builders;

public interface IShellContainerFactory
{
    Task<IServiceProvider> CreateContainerAsync(ShellSettings settings, ShellBlueprint blueprint);
}
