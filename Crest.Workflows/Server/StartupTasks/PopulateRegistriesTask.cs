using Crest.Workflows.Runtime;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace Crest.Workflows.StartupTasks;

public class PopulateRegistriesTask : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var registriesPopulator = scope.ServiceProvider.GetRequiredService<IRegistriesPopulator>();
            await registriesPopulator.PopulateAsync();

            // Shipped flows can only be read once the activity types they name are registered.
            await scope.ServiceProvider.GetRequiredService<Crest.Workflows.Registry.WorkflowFlowImporter>().SyncAsync();
        });

        // Background jobs (external calls) a previous process scheduled but never finished.
        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IBackgroundActivityScheduler>() is Crest.Workflows.Units.DurableBackgroundActivityScheduler durable ? durable.RecoverAsync() : Task.CompletedTask);
        return Task.CompletedTask;
    }
}