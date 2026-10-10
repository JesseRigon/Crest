using Crest.Workflows.Common.Multitenancy.HostedServices;
using Crest.Workflows.Mediator.HostedServices;
using Microsoft.Extensions.DependencyInjection;
using Crest.Environment.Shell.Scope;
using Crest.Modules;

namespace Crest.Workflows.StartupTasks;

public class StartHostedServices : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var activateTenants = scope.ServiceProvider.GetRequiredService<ActivateTenants>();
            var jobRunnerHostedService = scope.ServiceProvider.GetRequiredService<JobRunnerHostedService>();
            await activateTenants.StartAsync(CancellationToken.None);
            await jobRunnerHostedService.StartAsync(CancellationToken.None);
            // The engine's background command and notification channels, consumed in shell scopes.
            scope.ServiceProvider.GetRequiredService<Crest.Workflows.Units.ShellScopedBackgroundConsumers>().Start();
        });
        return Task.CompletedTask;
    }

    public override async Task TerminatingAsync()
    {
        var activateTenants = ShellScope.Services.GetRequiredService<ActivateTenants>();
        var jobRunnerHostedService = ShellScope.Services.GetRequiredService<JobRunnerHostedService>();
        await activateTenants.StopAsync(CancellationToken.None);
        await jobRunnerHostedService.StopAsync(CancellationToken.None);
        await ShellScope.Services.GetRequiredService<Crest.Workflows.Units.ShellScopedBackgroundConsumers>().StopAsync();
    }
}