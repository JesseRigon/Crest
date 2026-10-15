using Grove.Workflows.Common.Multitenancy;
using Grove.Workflows.Mediator;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Tenants.Mediator;
using Grove.Workflows.Runtime.Commands;
using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Runtime;

/// <summary>
/// A simple implementation that queues the specified request for delivering stimuli on a non-durable background worker.
/// </summary>
public class BackgroundStimulusDispatcher(ICommandSender commandSender, ITenantAccessor tenantAccessor) : IStimulusDispatcher
{
    /// <inheritdoc />
    public async Task<DispatchStimulusResponse> SendAsync(DispatchStimulusRequest request, CancellationToken cancellationToken = default)
    {
        var command = new DispatchStimulusCommand(request);
        await commandSender.SendAsync(command, CommandStrategy.Background, CreateHeaders(), cancellationToken);
        return DispatchStimulusResponse.Empty;
    }

    private IDictionary<object, object> CreateHeaders()
    {
        return TenantHeaders.CreateHeaders(tenantAccessor.Tenant?.Id);
    }
}
