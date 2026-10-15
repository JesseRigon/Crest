using Grove.Workflows.Common.Multitenancy;
using Grove.Workflows.Mediator;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Tenants.Mediator;
using Grove.Workflows.Runtime.Commands;
using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Runtime;

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