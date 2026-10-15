using Grove.Workflows.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Shell.Scope;
using Grove.Modules;

namespace Grove.Workflows.StartupTasks;

public class PopulateRegistriesTask : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var registriesPopulator = scope.ServiceProvider.GetRequiredService<IRegistriesPopulator>();
            await registriesPopulator.PopulateAsync();

            // Shipped flows can only be read once the activity types they name are registered.
            await scope.ServiceProvider.GetRequiredService<Grove.Workflows.Registry.WorkflowFlowImporter>().SyncAsync();
        });

        // Background jobs (external calls) a previous process scheduled but never finished.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IBackgroundActivityScheduler>() is Grove.Workflows.Units.DurableBackgroundActivityScheduler durable ? durable.RecoverAsync() : Task.CompletedTask);

        // Stimuli a previous process committed but never delivered (the event queue is at least once).
        ShellScope.AddDeferredTask(scope => Grove.Workflows.Units.WorkflowStimulusQueue.RecoverAsync(scope.ServiceProvider.GetRequiredService<YesSql.ISession>()));
        return Task.CompletedTask;
    }
}