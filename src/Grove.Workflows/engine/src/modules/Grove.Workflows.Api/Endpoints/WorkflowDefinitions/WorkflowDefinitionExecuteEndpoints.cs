using System.Dynamic;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Expressions.Helpers;
using Grove.Workflows.Management;
using Grove.Workflows.Models;
using Grove.Workflows.Runtime;
using Grove.Workflows.Serialization.Converters;
using Grove.Workflows.State;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api.Endpoints.WorkflowDefinitions;

/// <summary>
/// Executes a definition synchronously and answers its state: by GET with the input in the
/// query string, by POST with it in the body. The answer is written as the run goes, so a
/// workflow that writes the response itself (an HTTP endpoint flow) is not answered twice.
/// </summary>
public static class WorkflowDefinitionExecuteEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/workflow-definitions/{definitionId}/execute", ExecuteFromQueryAsync).RequireAuthorization(EngineApiPolicies.ExecuteWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/execute", ExecuteAsync).RequireAuthorization(EngineApiPolicies.ExecuteWorkflowDefinitions);
    }

    private static async Task<IResult> ExecuteFromQueryAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [AsParameters] ExecuteWorkflowQuery query,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        [FromServices] IWorkflowRuntime workflowRuntime,
        [FromServices] IWorkflowStarter workflowStarter,
        [FromServices] IApiSerializer apiSerializer,
        CancellationToken cancellationToken)
    {
        var request = new StartWorkflowRequest
        {
            CorrelationId = query.CorrelationId,
            Name = query.Name,
            Input = ParseDictionary(query.Input),
            Variables = ParseDictionary(query.Variables),
            TriggerActivityId = query.TriggerActivityId,
            ActivityHandle = string.IsNullOrWhiteSpace(query.ActivityHandle) ? null : JsonSerializer.Deserialize<ActivityHandle>(query.ActivityHandle, apiSerializer.GetOptions())
        };

        return await ExecuteCoreAsync(context, definitionId, query.VersionOptions, request, workflowDefinitionService, workflowRuntime, workflowStarter, apiSerializer, cancellationToken);
    }

    private static async Task<IResult> ExecuteAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        EngineBody<ExecuteWorkflowRequest> body,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        [FromServices] IWorkflowRuntime workflowRuntime,
        [FromServices] IWorkflowStarter workflowStarter,
        [FromServices] IApiSerializer apiSerializer,
        CancellationToken cancellationToken)
    {
        var model = body.Value;
        var request = new StartWorkflowRequest
        {
            CorrelationId = model.CorrelationId,
            Name = model.Name,
            Input = (IDictionary<string, object>?)model.Input,
            Variables = (IDictionary<string, object>?)model.Variables,
            TriggerActivityId = model.TriggerActivityId,
            ActivityHandle = model.ActivityHandle
        };

        return await ExecuteCoreAsync(context, definitionId, model.VersionOptions, request, workflowDefinitionService, workflowRuntime, workflowStarter, apiSerializer, cancellationToken);
    }

    private static async Task<IResult> ExecuteCoreAsync(
        HttpContext context,
        string definitionId,
        VersionOptions? versionOptions,
        StartWorkflowRequest request,
        IWorkflowDefinitionService workflowDefinitionService,
        IWorkflowRuntime workflowRuntime,
        IWorkflowStarter workflowStarter,
        IApiSerializer apiSerializer,
        CancellationToken cancellationToken)
    {
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(definitionId, versionOptions ?? VersionOptions.Published, cancellationToken);
        if (workflowGraph == null)
        {
            return EngineResults.NotFound();
        }

        request.Workflow = workflowGraph.Workflow;
        var response = context.Response;
        var startResponse = await workflowStarter.StartWorkflowAsync(request, cancellationToken);
        if (!response.HasStarted)
        {
            response.Headers.Append("x-grove-workflows-workflow-cannot-start", startResponse.CannotStart.ToString());
        }

        if (startResponse.CannotStart)
        {
            return response.HasStarted ? TypedResults.Empty : EngineResults.Ok();
        }

        var instanceId = startResponse.WorkflowInstanceId!;
        if (!response.HasStarted)
        {
            response.Headers.Append("x-grove-workflows-workflow-instance-id", instanceId);
        }

        var workflowClient = await workflowRuntime.CreateClientAsync(instanceId, cancellationToken);
        if (startResponse.SubStatus == WorkflowSubStatus.Faulted)
        {
            var faultedState = await workflowClient.ExportStateAsync(cancellationToken);
            return TypedResults.Content(apiSerializer.Serialize(new ExecuteWorkflowResponse(faultedState)), MediaTypeNames.Application.Json, statusCode: StatusCodes.Status500InternalServerError);
        }

        if (response.HasStarted)
        {
            return TypedResults.Empty;
        }

        response.Headers.Append("x-grove-workflows-response", "true");
        if (response.StatusCode != StatusCodes.Status200OK)
        {
            return TypedResults.Empty;
        }

        var workflowState = await workflowClient.ExportStateAsync(cancellationToken);
        return TypedResults.Content(apiSerializer.Serialize(new ExecuteWorkflowResponse(workflowState)), MediaTypeNames.Application.Json);
    }

    private static IDictionary<string, object>? ParseDictionary(string? value)
    {
        var result = value?.TryConvertTo<ExpandoObject>(new()
        {
            SerializerOptions = new()
            {
                Converters = { new ExpandoObjectConverter() }
            }
        });

        return result?.IsSuccess == true ? (IDictionary<string, object>?)result.Value : null;
    }
}

/// <summary>The GET form: input and variables as JSON strings.</summary>
public class ExecuteWorkflowQuery
{
    [FromQuery] public string? CorrelationId { get; set; }

    [FromQuery] public string? Name { get; set; }

    [FromQuery] public string? TriggerActivityId { get; set; }

    /// <summary>An <see cref="Models.ActivityHandle"/> as JSON.</summary>
    [FromQuery] public string? ActivityHandle { get; set; }

    [FromQuery] public VersionOptions? VersionOptions { get; set; }

    [FromQuery] public string? Input { get; set; }

    [FromQuery] public string? Variables { get; set; }
}

public class ExecuteWorkflowRequest
{
    public string? CorrelationId { get; set; }

    public string? Name { get; set; }

    public string? TriggerActivityId { get; set; }

    public ActivityHandle? ActivityHandle { get; set; }

    public VersionOptions? VersionOptions { get; set; }

    [JsonConverter(typeof(ExpandoObjectConverterFactory))]
    public object? Input { get; set; }

    [JsonConverter(typeof(ExpandoObjectConverterFactory))]
    public object? Variables { get; set; }
}

public class ExecuteWorkflowResponse(WorkflowState workflowState)
{
    public WorkflowState WorkflowState { get; } = workflowState;
}
