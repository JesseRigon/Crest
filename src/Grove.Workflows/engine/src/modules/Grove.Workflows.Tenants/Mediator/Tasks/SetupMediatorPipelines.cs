using Grove.Workflows.Mediator.Middleware.Command;
using Grove.Workflows.Mediator.Middleware.Command.Contracts;
using Grove.Workflows.Tenants.Mediator.Middleware;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace Grove.Workflows.Tenants.Mediator.Tasks;

[UsedImplicitly]
public class SetupMediatorPipelines(ICommandPipeline commandPipeline) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        commandPipeline.Setup(pipeline => pipeline.UseMiddleware<TenantPropagatingMiddleware>(0));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}