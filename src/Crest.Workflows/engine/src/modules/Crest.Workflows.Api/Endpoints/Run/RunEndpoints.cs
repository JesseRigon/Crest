using System.Text.Json.Serialization;
using Crest.Workflows.Management;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.SasTokens.Contracts;
using Crest.Workflows.Serialization.Converters;
using Crest.Workflows.State;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Crest.Workflows.Api.Endpoints.Run;

/// <summary>What resumes or starts a run from outside: bookmarks, events, task completion, and the activity test runner.</summary>
public static class RunEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bookmarks/resume", ResumeBookmarkFromQueryAsync).AllowAnonymous();
        routes.MapPost("/bookmarks/resume", ResumeBookmarkAsync).AllowAnonymous();
        routes.MapGet("/events/trigger", TriggerPublicEventAsync).AllowAnonymous();
        routes.MapPost("/events/{eventName}/trigger", TriggerEventAsync).RequireAuthorization(EngineApiPolicies.TriggerEvents);
        routes.MapPost("/tasks/{taskId}/complete", CompleteTaskAsync).RequireAuthorization(EngineApiPolicies.CompleteTasks);
        routes.MapPost("/tests/activities", TestActivityAsync).RequireAuthorization(EngineApiPolicies.ExecuteTests);
    }

    /// <summary>Resumes the bookmark named by the SAS token <c>t</c>; the input comes as JSON in <c>in</c>.</summary>
    private static async Task<IResult> ResumeBookmarkFromQueryAsync(
        HttpContext context,
        [FromQuery(Name = "t")] string? token,
        [FromQuery(Name = "async")] bool? asynchronous,
        [FromQuery(Name = "in")] string? inputJson,
        [FromServices] ITokenService tokenService,
        [FromServices] IWorkflowResumer workflowResumer,
        [FromServices] IBookmarkQueue bookmarkQueue,
        [FromServices] IPayloadSerializer serializer)
    {
        var errors = new List<string>();
        if (!tokenService.TryDecryptToken<BookmarkTokenPayload>(token ?? string.Empty, out var payload))
        {
            errors.Add("Invalid token.");
        }

        IDictionary<string, object>? input = null;
        if (!string.IsNullOrWhiteSpace(inputJson))
        {
            try
            {
                input = serializer.Deserialize<IDictionary<string, object>>(inputJson);
            }
            catch
            {
                errors.Add("Invalid input format. Expected a valid JSON string.");
            }
        }

        if (errors.Count > 0)
        {
            return EngineResults.Errors(errors);
        }

        await ResumeAsync(payload!, input, asynchronous == true, workflowResumer, bookmarkQueue);
        return context.Response.HasStarted ? TypedResults.Empty : EngineResults.Ok();
    }

    /// <summary>Resumes the bookmark named by the SAS token <c>t</c>; the input comes in the body.</summary>
    private static async Task<IResult> ResumeBookmarkAsync(
        HttpContext context,
        [FromQuery(Name = "t")] string? token,
        [FromQuery(Name = "async")] bool? asynchronous,
        EngineBody<ResumeBookmarkRequestBody> body,
        [FromServices] ITokenService tokenService,
        [FromServices] IWorkflowResumer workflowResumer,
        [FromServices] IBookmarkQueue bookmarkQueue)
    {
        if (!tokenService.TryDecryptToken<BookmarkTokenPayload>(token ?? string.Empty, out var payload))
        {
            return EngineResults.Errors("Invalid token.");
        }

        await ResumeAsync(payload, body.Value.Input, asynchronous == true, workflowResumer, bookmarkQueue);
        return context.Response.HasStarted ? TypedResults.Empty : EngineResults.Ok();
    }

    private static async Task ResumeAsync(BookmarkTokenPayload tokenPayload, IDictionary<string, object>? input, bool asynchronous, IWorkflowResumer workflowResumer, IBookmarkQueue bookmarkQueue)
    {
        // Some clients, like Blazor, cancel their request when navigating away; the resume is
        // not cancelled with it.
        var cancellationToken = CancellationToken.None;
        if (asynchronous)
        {
            await bookmarkQueue.EnqueueAsync(new NewBookmarkQueueItem
            {
                BookmarkId = tokenPayload.BookmarkId,
                WorkflowInstanceId = tokenPayload.WorkflowInstanceId,
                Options = new() { Input = input }
            }, cancellationToken);
            return;
        }

        await workflowResumer.ResumeAsync(new ResumeBookmarkRequest
        {
            BookmarkId = tokenPayload.BookmarkId,
            WorkflowInstanceId = tokenPayload.WorkflowInstanceId,
            Input = input
        }, cancellationToken);
    }

    /// <summary>Resumes the instance blocked on the event named by the SAS token <c>t</c>.</summary>
    private static async Task<IResult> TriggerPublicEventAsync(
        HttpContext context,
        [FromQuery(Name = "t")] string? token,
        [FromServices] ITokenService tokenService,
        [FromServices] IEventPublisher eventPublisher,
        CancellationToken cancellationToken)
    {
        if (!tokenService.TryDecryptToken<EventTokenPayload>(token ?? string.Empty, out var payload))
        {
            return EngineResults.Errors("Invalid token.");
        }

        await eventPublisher.PublishAsync(payload.EventName, workflowInstanceId: payload.WorkflowInstanceId, cancellationToken: cancellationToken);
        return context.Response.HasStarted ? TypedResults.Empty : EngineResults.Ok();
    }

    /// <summary>Triggers every workflow waiting for the event.</summary>
    private static async Task<IResult> TriggerEventAsync(
        HttpContext context,
        [FromRoute] string eventName,
        EngineBody<TriggerEventRequest> body,
        [FromServices] IEventPublisher eventPublisher,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var input = (IDictionary<string, object>?)request.Input;
        var asynchronous = request.WorkflowExecutionMode == WorkflowExecutionMode.Asynchronous;
        await eventPublisher.PublishAsync(eventName, request.CorrelationId, request.WorkflowInstanceId, request.ActivityInstanceId, input, asynchronous, cancellationToken);
        return context.Response.HasStarted ? TypedResults.Empty : EngineResults.Ok();
    }

    /// <summary>Resumes the <c>RunTask</c> activity waiting on the task.</summary>
    private static async Task<IResult> CompleteTaskAsync(
        HttpContext context,
        [FromRoute] string taskId,
        EngineBody<CompleteTaskRequest> body,
        [FromServices] ITaskReporter taskReporter,
        CancellationToken cancellationToken)
    {
        await taskReporter.ReportCompletionAsync(taskId, body.Value.Result, cancellationToken);
        return context.Response.HasStarted ? TypedResults.Empty : EngineResults.Ok();
    }

    /// <summary>Runs one activity of a definition on its own and answers its outcome.</summary>
    private static async Task<IResult> TestActivityAsync(
        HttpContext context,
        EngineBody<TestActivityRequest> body,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        [FromServices] IActivityTestRunner activityTestRunner,
        [FromServices] IActivityExecutionMapper activityExecutionMapper,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var workflowGraph = request.WorkflowDefinitionHandle == null ? null : await workflowDefinitionService.FindWorkflowGraphAsync(request.WorkflowDefinitionHandle, cancellationToken);
        if (workflowGraph == null)
        {
            return EngineResults.Errors("Workflow definition not found.");
        }

        var activity = request.ActivityHandle == null ? null : workflowGraph.FindActivity(request.ActivityHandle);
        if (activity == null)
        {
            return EngineResults.Errors("Activity not found.");
        }

        var activityExecutionContext = await activityTestRunner.RunAsync(workflowGraph, activity, cancellationToken);
        var record = await activityExecutionMapper.MapAsync(activityExecutionContext);
        return EngineResults.Json(context, new TestActivityResponse
        {
            ActivityState = record.ActivityState ?? new Dictionary<string, object?>(),
            Outputs = record.Outputs,
            Payload = record.Payload,
            Exception = record.Exception,
            Status = record.Status
        });
    }
}

public class ResumeBookmarkRequestBody
{
    /// <summary>The input to provide to the workflow when resuming.</summary>
    public IDictionary<string, object>? Input { get; set; }
}

public class TriggerEventRequest
{
    public string? WorkflowInstanceId { get; set; }

    public string? CorrelationId { get; set; }

    public string? ActivityInstanceId { get; set; }

    [JsonConverter(typeof(ExpandoObjectConverterFactory))]
    public object? Input { get; set; }

    public WorkflowExecutionMode WorkflowExecutionMode { get; set; } = WorkflowExecutionMode.Asynchronous;
}

public class CompleteTaskRequest
{
    public object? Result { get; set; }
}

public class TestActivityRequest
{
    public WorkflowDefinitionHandle? WorkflowDefinitionHandle { get; set; }

    public ActivityHandle? ActivityHandle { get; set; }
}

public class TestActivityResponse
{
    public IDictionary<string, object?> ActivityState { get; set; } = null!;

    public IDictionary<string, object?>? Outputs { get; set; }

    public IDictionary<string, object>? Payload { get; set; }

    public ExceptionState? Exception { get; set; }

    public ActivityStatus Status { get; set; }
}
