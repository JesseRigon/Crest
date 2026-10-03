using Crest.Workflows.Common;
using Crest.Workflows.Models;
using Crest.Workflows.Pipelines.WorkflowExecution;
using Crest.Workflows.State;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Middleware.Workflows;

/// <summary>
/// Adds extension methods to <see cref="ExceptionHandlingMiddleware"/>.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    /// <summary>
    /// Installs the <see cref="ExceptionHandlingMiddleware"/> component in the activity execution pipeline.
    /// </summary>
    public static IWorkflowExecutionPipelineBuilder UseExceptionHandling(this IWorkflowExecutionPipelineBuilder pipelineBuilder) => pipelineBuilder.UseMiddleware<ExceptionHandlingMiddleware>();
}

/// <summary>
/// Catches any exceptions thrown by downstream components and transitions the workflow into the faulted state.
/// </summary>
public class ExceptionHandlingMiddleware : IWorkflowExecutionMiddleware
{
    private readonly WorkflowMiddlewareDelegate _next;
    private readonly ISystemClock _systemClock;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ExceptionHandlingMiddleware(WorkflowMiddlewareDelegate next, ISystemClock systemClock, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _systemClock = systemClock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask InvokeAsync(WorkflowExecutionContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "An exception was caught from a downstream middleware component");
            var exceptionState = ExceptionState.FromException(e);
            var now = _systemClock.UtcNow;
            var activity = context.Workflow;
            var incident = new ActivityIncident(activity.Id, activity.NodeId ,activity.Type, e.Message, exceptionState, now);
            context.Incidents.Add(incident);
            context.TransitionTo(WorkflowSubStatus.Faulted);
            context.AddExecutionLogEntry("Faulted", e.Message, exceptionState);
        }
    }
}