using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Mediator;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Tenants.Mediator;
using Crest.Workflows.Runtime.Commands;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime;

/// <summary>
///     Dispatches workflow cancellation requests to a local background worker.
/// </summary>
public class BackgroundWorkflowCancellationDispatcher(ICommandSender commandSender, ITenantAccessor tenantAccessor) : IWorkflowCancellationDispatcher
{
    /// <inheritdoc />
    public async Task<DispatchCancelWorkflowsResponse> DispatchAsync(DispatchCancelWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var command = new CancelWorkflowsCommand(request);
        await commandSender.SendAsync(command, CommandStrategy.Background, CreateHeaders(), cancellationToken);
        return new DispatchCancelWorkflowsResponse();
    }

    private IDictionary<object, object> CreateHeaders()
    {
        return TenantHeaders.CreateHeaders(tenantAccessor.Tenant?.Id);
    }
}