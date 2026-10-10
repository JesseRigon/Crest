using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Mediator;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Tenants.Mediator;
using Crest.Workflows.Runtime.Commands;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime;

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
