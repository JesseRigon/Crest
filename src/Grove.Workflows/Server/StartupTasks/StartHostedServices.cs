using Grove.Workflows.Common.Multitenancy.HostedServices;
using Grove.Workflows.Mediator.HostedServices;
using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Shell.Scope;
using Grove.Modules;

namespace Grove.Workflows.StartupTasks;

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
            scope.ServiceProvider.GetRequiredService<Grove.Workflows.Units.ShellScopedBackgroundConsumers>().Start();
        });
        return Task.CompletedTask;
    }

    public override async Task TerminatingAsync()
    {
        var activateTenants = ShellScope.Services.GetRequiredService<ActivateTenants>();
        var jobRunnerHostedService = ShellScope.Services.GetRequiredService<JobRunnerHostedService>();
        await activateTenants.StopAsync(CancellationToken.None);
        await jobRunnerHostedService.StopAsync(CancellationToken.None);
        await ShellScope.Services.GetRequiredService<Grove.Workflows.Units.ShellScopedBackgroundConsumers>().StopAsync();
    }
}