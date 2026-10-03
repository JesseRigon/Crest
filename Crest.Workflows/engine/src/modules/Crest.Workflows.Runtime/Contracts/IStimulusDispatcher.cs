using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime;

public interface IStimulusDispatcher
{
    /// <summary>
    /// Dispatches stimulus. This could result in new workflow instances being started as well as existing workflow instances being resumed.
    /// </summary>
    Task<DispatchStimulusResponse> SendAsync(DispatchStimulusRequest request, CancellationToken cancellationToken = default);
}