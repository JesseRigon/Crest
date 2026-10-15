using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Runtime;

public interface IStimulusDispatcher
{
    /// <summary>
    /// Dispatches stimulus. This could result in new workflow instances being started as well as existing workflow instances being resumed.
    /// </summary>
    Task<DispatchStimulusResponse> SendAsync(DispatchStimulusRequest request, CancellationToken cancellationToken = default);
}