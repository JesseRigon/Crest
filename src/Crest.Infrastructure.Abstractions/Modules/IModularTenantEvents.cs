using Crest.Environment.Shell.Removing;

namespace Crest.Modules;

public interface IModularTenantEvents
{
    Task ActivatingAsync();
    Task ActivatedAsync();
    Task TerminatingAsync();
    Task TerminatedAsync();
    Task RemovingAsync(ShellRemovingContext context);
}
