using Crest.Workflows.Abstractions;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Activities;

namespace Crest.Workflows.Api.Endpoints.Tasks.Complete;

/// <summary>
/// Resumes the <see cref="RunTask"/> activity matching the received Task ID.
/// </summary>
public class Complete : WorkflowsEndpoint<Request, Response>
{
    private readonly ITaskReporter _taskReporter;

    /// <inheritdoc />
    public Complete(ITaskReporter taskReporter)
    {
        _taskReporter = taskReporter;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/tasks/{taskId}/complete");
        ConfigurePermissions("tasks:complete");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        await _taskReporter.ReportCompletionAsync(request.TaskId, request.Result, cancellationToken);
        if (!HttpContext.Response.HasStarted) await Send.OkAsync(cancellation: cancellationToken);
    }
}
