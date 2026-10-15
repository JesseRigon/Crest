using Grove.Environment.Shell.Removing;

namespace Grove.Modules;

public interface IModularTenantEvents
{
    Task ActivatingAsync();
    Task ActivatedAsync();
    Task TerminatingAsync();
    Task TerminatedAsync();
    Task RemovingAsync(ShellRemovingContext context);
}
