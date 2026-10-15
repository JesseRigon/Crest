using Grove.Workflows.Runtime.Options;
using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Runtime;

/// <summary>
/// Validates the workflow request before dispatching it to the workflow dispatcher.
/// </summary>
/// <param name="decoratedService">The workflow dispatcher to decorate.</param>
/// <param name="dispatcherOptions">The workflow dispatcher options.</param>
public class ValidatingWorkflowDispatcher(IWorkflowDispatcher decoratedService, IOptions<WorkflowDispatcherOptions> dispatcherOptions) : IWorkflowDispatcher
{
    private IWorkflowDispatcher DecoratedService { get; set; } = decoratedService;

    /// <inheritdoc />
    public async Task<DispatchWorkflowResponse> DispatchAsync(DispatchWorkflowDefinitionRequest request, DispatchWorkflowOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!ValidateChannel(options?.Channel))
            return DispatchWorkflowResponse.UnknownChannel();

        return await DecoratedService.DispatchAsync(request, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DispatchWorkflowResponse> DispatchAsync(DispatchWorkflowInstanceRequest request, DispatchWorkflowOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!ValidateChannel(options?.Channel))
            return DispatchWorkflowResponse.UnknownChannel();

        return await DecoratedService.DispatchAsync(request, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DispatchWorkflowResponse> DispatchAsync(DispatchTriggerWorkflowsRequest request, DispatchWorkflowOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!ValidateChannel(options?.Channel))
            return DispatchWorkflowResponse.UnknownChannel();

        return await DecoratedService.DispatchAsync(request, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DispatchWorkflowResponse> DispatchAsync(DispatchResumeWorkflowsRequest request, DispatchWorkflowOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!ValidateChannel(options?.Channel))
            return DispatchWorkflowResponse.UnknownChannel();

        return await DecoratedService.DispatchAsync(request, options, cancellationToken);
    }

    private bool ValidateChannel(string? channelName)
    {
        return string.IsNullOrEmpty(channelName) || GetChannelExists(channelName);
    }

    private bool GetChannelExists(string channelName)
    {
        return dispatcherOptions.Value.Channels.Any(x => x.Name == channelName);
    }
}