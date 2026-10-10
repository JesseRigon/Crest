namespace Crest.Workflows.Runtime;

public interface ITriggerInvoker
{
    Task<StartWorkflowResponse> InvokeAsync(InvokeTriggerRequest request, CancellationToken cancellationToken = default);
}