using Crest.Workflows.Mediator.Middleware.Command;
using Crest.Workflows.Mediator.Middleware.Command.Contracts;
using Crest.Workflows.Tenants.Mediator.Middleware;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace Crest.Workflows.Tenants.Mediator.Tasks;

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