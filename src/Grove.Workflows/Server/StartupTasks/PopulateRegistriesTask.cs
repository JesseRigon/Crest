using Grove.Environment.Shell.Scope;
using Grove.Modules;
using Grove.Workflows.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.StartupTasks;

/// <summary>
/// Fills the workflow registries and brings the shipped flows in when the tenant activates
/// (docs/workflows.md › Shipped flows), through <see cref="Registry.ShippedFlowsInstaller"/>: in a
/// task deferred to the end of the activation scope, so after the migrations of that activation
/// have committed and before the request or recipe step that activated the shell runs. A content
/// hook that finds a system flow missing installs them on demand first.
/// </summary>
public class PopulateRegistriesTask : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<Registry.ShippedFlowsInstaller>().EnsureAsync(scope.ServiceProvider));

        // Background jobs (external calls) a previous process scheduled but never finished.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IBackgroundActivityScheduler>() is Grove.Workflows.Units.DurableBackgroundActivityScheduler durable ? durable.RecoverAsync() : Task.CompletedTask);

        // Stimuli a previous process committed but never delivered (the event queue is at least once).
        ShellScope.AddDeferredTask(scope => Grove.Workflows.Units.WorkflowStimulusQueue.RecoverAsync(scope.ServiceProvider.GetRequiredService<YesSql.ISession>()));
        return Task.CompletedTask;
    }
}
