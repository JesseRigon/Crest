using Grove.Access;
using Grove.Workflows.Contexts;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Security;

/// <summary>
/// The actor of a dispatched run is the dispatcher's caller, never what the request's input
/// says (docs/workflows.md › The actor rule): a new run - a definition or a trigger dispatch
/// from the API, a <c>DispatchWorkflow</c> activity, a trigger publisher - carries the caller
/// of the request or burst that dispatched it, sealed; a resume carries no actor at all, since
/// the run keeps the one bound to its instance. A dispatch with no caller keeps only an actor
/// the server sealed (a trigger captured in a background scope).
/// </summary>
public sealed class ActorStampingWorkflowDispatcher(IWorkflowDispatcher inner, ICallerContextAccessor callers, WorkflowActorSeal seal) : IWorkflowDispatcher
{
    public Task<DispatchWorkflowResponse> DispatchAsync(DispatchWorkflowDefinitionRequest request, DispatchWorkflowOptions? options, CancellationToken cancellationToken = default)
    {
        request.Input = Stamped(request.Input);
        return inner.DispatchAsync(request, options, cancellationToken);
    }

    public Task<DispatchWorkflowResponse> DispatchAsync(DispatchWorkflowInstanceRequest request, DispatchWorkflowOptions? options, CancellationToken cancellationToken = default)
    {
        if (request.Input?.ContainsKey(WorkflowsConstants.InputKeys.Actor) == true)
        {
            request = new DispatchWorkflowInstanceRequest(request.InstanceId)
            {
                BookmarkId = request.BookmarkId,
                ActivityHandle = request.ActivityHandle,
                Input = WithoutActor(request.Input),
                Properties = request.Properties,
                CorrelationId = request.CorrelationId,
            };
        }

        return inner.DispatchAsync(request, options, cancellationToken);
    }

    public Task<DispatchWorkflowResponse> DispatchAsync(DispatchTriggerWorkflowsRequest request, DispatchWorkflowOptions? options, CancellationToken cancellationToken = default)
    {
        request.Input = Stamped(request.Input);
        return inner.DispatchAsync(request, options, cancellationToken);
    }

    public Task<DispatchWorkflowResponse> DispatchAsync(DispatchResumeWorkflowsRequest request, DispatchWorkflowOptions? options, CancellationToken cancellationToken = default)
    {
        if (request.Input?.ContainsKey(WorkflowsConstants.InputKeys.Actor) == true)
        {
            request = request with { Input = WithoutActor(request.Input) };
        }

        return inner.DispatchAsync(request, options, cancellationToken);
    }

    private IDictionary<string, object>? Stamped(IDictionary<string, object>? input)
    {
        if (callers.Current is not { } caller)
        {
            return input;
        }

        var stamped = input is null ? new Dictionary<string, object>() : new Dictionary<string, object>(input);
        if (caller.IsSystem)
        {
            // The system caller is never an actor: a dispatched flow runs as system only when
            // its own definition is published as system.
            stamped.Remove(WorkflowsConstants.InputKeys.Actor);
        }
        else
        {
            stamped[WorkflowsConstants.InputKeys.Actor] = seal.Seal(WorkflowUserContext.From(caller));
        }

        return stamped;
    }

    private static Dictionary<string, object> WithoutActor(IDictionary<string, object> input)
    {
        var copy = new Dictionary<string, object>(input);
        copy.Remove(WorkflowsConstants.InputKeys.Actor);
        return copy;
    }
}
