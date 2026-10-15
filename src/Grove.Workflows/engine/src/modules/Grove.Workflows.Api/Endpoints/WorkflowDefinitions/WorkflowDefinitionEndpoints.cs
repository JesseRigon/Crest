using System.Text.Json;
using Grove.Workflows.Activities;
using Grove.Workflows.Api.Constants;
using Grove.Workflows.Api.Models;
using Grove.Workflows.Api.Requirements;
using Grove.Workflows.Common.Entities;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Extensions;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Contracts;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Mappers;
using Grove.Workflows.Management.Materializers;
using Grove.Workflows.Management.Models;
using Grove.Workflows.Models;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;
using Grove.Workflows.Serialization.Converters;
using Grove.Workflows.Serialization.Helpers;
using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api.Endpoints.WorkflowDefinitions;

/// <summary>
/// The workflow definition operations: reads, the save, publish and retract cycle, versions,
/// references, dispatch and the bulk actions. Execute, export and import have their own files.
/// </summary>
public static class WorkflowDefinitionEndpoints
{
    public const string GetByIdRouteName = "GetWorkflowDefinitionById";

    public const string RefreshRouteName = "RefreshWorkflowDefinitions";

    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/workflow-definitions", ListAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/query/count", CountAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/many-by-id", GetManyByIdAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/by-id/{id}", GetByIdAsync).WithName(GetByIdRouteName).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/by-definition-id/{definitionId}", GetByDefinitionIdAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/{definitionId}", GetByDefinitionIdAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/validation/is-name-unique", IsNameUniqueAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/{definitionId}/consumers", GetConsumersAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/{definitionId}/versions", ListVersionsAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/subgraph/{id}", GetSubgraphAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/subgraph/segments/{id}", GetSubgraphSegmentsAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);

        routes.MapPost("/workflow-definitions", SaveAsync).RequireAuthorization(EngineApiPolicies.WriteWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/publish", PublishAsync).RequireAuthorization(EngineApiPolicies.PublishWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/retract", RetractAsync).RequireAuthorization(EngineApiPolicies.RetractWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/update-references", UpdateReferencesAsync).RequireAuthorization(EngineApiPolicies.PublishWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/revert/{version}", RevertVersionAsync).RequireAuthorization(EngineApiPolicies.PublishWorkflowDefinitions);
        routes.MapDelete("/workflow-definitions/{definitionId}", DeleteAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowDefinitions);
        routes.MapDelete("/workflow-definitions/{definitionId}/version/{version}", DeleteVersionAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowDefinitions);
        routes.MapDelete("/workflow-definition-versions/{id}", DeleteVersionByIdAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowDefinitions);

        routes.MapPost("/workflow-definitions/{definitionId}/dispatch", DispatchAsync).RequireAuthorization(EngineApiPolicies.ExecuteWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/bulk-dispatch", BulkDispatchAsync).RequireAuthorization(EngineApiPolicies.ExecuteWorkflowDefinitions);
        routes.MapPost("/actions/workflow-definitions/refresh", RefreshAsync).WithName(RefreshRouteName).RequireAuthorization(EngineApiPolicies.RefreshWorkflowDefinitions);
        routes.MapPost("/actions/workflow-definitions/reload", ReloadAsync).RequireAuthorization(EngineApiPolicies.ReloadWorkflowDefinitions);

        routes.MapPost("/bulk-actions/delete/workflow-definitions/by-definition-id", BulkDeleteAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowDefinitions);
        routes.MapPost("/bulk-actions/delete/workflow-definitions/by-id", BulkDeleteVersionsAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowDefinitions);
        routes.MapPost("/bulk-actions/publish/workflow-definitions/by-definition-ids", BulkPublishAsync).RequireAuthorization(EngineApiPolicies.PublishWorkflowDefinitions);
        routes.MapPost("/bulk-actions/retract/workflow-definitions/by-definition-ids", BulkRetractAsync).RequireAuthorization(EngineApiPolicies.RetractWorkflowDefinitions);
    }

    /// <summary>Not-read-only: the management option and the definition's own read-only or system flag.</summary>
    internal static async Task<bool> MayEditAsync(HttpContext context, IAuthorizationService authorizationService, Management.Entities.WorkflowDefinition? definition = null)
    {
        var result = await authorizationService.AuthorizeAsync(context.User, new NotReadOnlyResource(definition), AuthorizationPolicies.NotReadOnlyPolicy);
        return result.Succeeded;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        [AsParameters] ListWorkflowDefinitionsQuery query,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionLinker linker,
        CancellationToken cancellationToken)
    {
        var pageArgs = PageArgs.FromPage(query.Page, query.PageSize);
        var filter = new WorkflowDefinitionFilter
        {
            IsSystem = query.IsSystem,
            VersionOptions = string.IsNullOrWhiteSpace(query.VersionOptions) ? default(VersionOptions?) : VersionOptions.FromString(query.VersionOptions),
            SearchTerm = query.SearchTerm?.Trim(),
            MaterializerName = query.MaterializerName,
            DefinitionIds = query.DefinitionIds,
            Ids = query.Ids
        };

        var orderBy = query.OrderBy ?? OrderByWorkflowDefinition.Name;
        var direction = orderBy == OrderByWorkflowDefinition.Name ? query.OrderDirection ?? OrderDirection.Ascending : query.OrderDirection ?? OrderDirection.Descending;
        var summaries = orderBy switch
        {
            OrderByWorkflowDefinition.Name => await store.FindSummariesAsync(filter, new WorkflowDefinitionOrder<string> { KeySelector = p => p.Name!, Direction = direction }, pageArgs, cancellationToken),
            OrderByWorkflowDefinition.Version => await store.FindSummariesAsync(filter, new WorkflowDefinitionOrder<int> { KeySelector = p => p.Version, Direction = direction }, pageArgs, cancellationToken),
            _ => await store.FindSummariesAsync(filter, new WorkflowDefinitionOrder<DateTimeOffset> { KeySelector = p => p.CreatedAt, Direction = direction }, pageArgs, cancellationToken),
        };

        var response = linker.MapAsync(new PagedListResponse<WorkflowDefinitionSummary>(summaries), cancellationToken);
        return EngineResults.Json(context, response);
    }

    private static async Task<IResult> CountAsync(HttpContext context, [FromServices] IWorkflowDefinitionStore store, CancellationToken cancellationToken) =>
        EngineResults.Json(context, new CountResponse(await store.CountDistinctAsync(cancellationToken)));

    private static async Task<IResult> GetManyByIdAsync(
        HttpContext context,
        [FromQuery] string[]? ids,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionLinker linker,
        CancellationToken cancellationToken)
    {
        var definitions = (await store.FindManyAsync(new WorkflowDefinitionFilter { Ids = ids ?? [] }, cancellationToken)).ToList();
        var models = await linker.MapAsync(definitions, cancellationToken);
        return EngineResults.Json(context, new ListResponse<LinkedWorkflowDefinitionModel>(models));
    }

    private static async Task<IResult> GetByIdAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionLinker linker,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { Id = id }, cancellationToken);
        return definition == null ? EngineResults.NotFound() : EngineResults.Json(context, await linker.MapAsync(definition, cancellationToken));
    }

    private static async Task<IResult> GetByDefinitionIdAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromQuery] string? versionOptions,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionLinker linker,
        CancellationToken cancellationToken)
    {
        var version = versionOptions != null ? VersionOptions.FromString(versionOptions) : VersionOptions.Latest;
        var definition = await store.FindAsync(WorkflowDefinitionHandle.ByDefinitionId(definitionId, version).ToFilter(), cancellationToken);
        return definition == null ? EngineResults.NotFound() : EngineResults.Json(context, await linker.MapAsync(definition, cancellationToken));
    }

    private static async Task<IResult> IsNameUniqueAsync(
        HttpContext context,
        [FromQuery] string name,
        [FromQuery] string? definitionId,
        [FromServices] IWorkflowDefinitionStore store,
        CancellationToken cancellationToken)
    {
        var isUnique = await store.GetIsNameUnique(name.Trim(), definitionId, cancellationToken);
        return EngineResults.Json(context, new IsNameUniqueResponse(isUnique));
    }

    /// <summary>Every definition that consumes the definition, recursively.</summary>
    private static async Task<IResult> GetConsumersAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowReferenceGraphBuilder workflowReferenceGraphBuilder,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
        if (definition == null)
        {
            return EngineResults.NotFound();
        }

        var graph = await workflowReferenceGraphBuilder.BuildGraphAsync(definitionId, cancellationToken);
        return EngineResults.Json(context, new ConsumersResponse(graph.ConsumerDefinitionIds.ToList()));
    }

    private static async Task<IResult> ListVersionsAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromServices] IWorkflowDefinitionStore store,
        CancellationToken cancellationToken)
    {
        var filter = new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.All };
        var orderBy = new WorkflowDefinitionOrder<int>(x => x.Version, OrderDirection.Descending);
        var definitions = (await store.FindManyAsync(filter, orderBy, cancellationToken)).ToList();
        return definitions.Count == 0 ? EngineResults.NotFound() : EngineResults.Json(context, definitions);
    }

    private static async Task<IResult> GetSubgraphAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromQuery] string? parentNodeId,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        [FromServices] ActivityWriter activityWriter,
        CancellationToken cancellationToken)
    {
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(id, cancellationToken);
        if (workflowGraph == null)
        {
            return EngineResults.NotFound();
        }

        var parentNode = parentNodeId != null && workflowGraph.NodeIdLookup.TryGetValue(parentNodeId, out var node) ? node : workflowGraph.Root;
        var serializerOptions = EngineResults.Options(context).Clone();
        serializerOptions.Converters.Add(new RootActivityNodeConverter(activityWriter));
        return EngineResults.Json(context, parentNode, serializerOptions);
    }

    /// <summary>The path of container segments from the root down to a node.</summary>
    private static async Task<IResult> GetSubgraphSegmentsAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromQuery] string? childNodeId,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        [FromServices] ActivityWriter activityWriter,
        CancellationToken cancellationToken)
    {
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(id, cancellationToken);
        if (workflowGraph?.Root == null)
        {
            return EngineResults.NotFound();
        }

        if (childNodeId == null || !workflowGraph.NodeIdLookup.TryGetValue(childNodeId, out var childNode))
        {
            return EngineResults.Errors("Unknown node ID");
        }

        var ancestors = childNode.Ancestors().ToList();
        var serializerOptions = EngineResults.Options(context).Clone().WithConverters(new RootActivityNodeConverter(activityWriter));
        var segments = new Stack<ActivityPathSegment>();
        var currentNode = ancestors.FirstOrDefault();
        var previousNode = childNode;
        while (currentNode != null)
        {
            if (currentNode.Activity is not Workflow and not Activities.Flowchart.Activities.Flowchart)
            {
                var currentActivity = currentNode.Activity;
                segments.Push(new ActivityPathSegment(currentNode.NodeId, currentActivity.Id, currentActivity.Type, previousNode.Port, currentActivity.Name ?? currentActivity.Type));
            }

            previousNode = currentNode;
            currentNode = currentNode.Parents.FirstOrDefault();
        }

        var leafSegment = segments.LastOrDefault();
        var container = leafSegment == null ? ancestors.LastOrDefault() ?? childNode : ancestors.Last(x => x.Activity.Id == leafSegment.ActivityId);
        return EngineResults.Json(context, new SubgraphSegmentsResponse(childNode, container, segments.ToList()), serializerOptions);
    }

    /// <summary>Saves the draft, and publishes it when asked.</summary>
    private static async Task<IResult> SaveAsync(
        HttpContext context,
        EngineBody<SaveWorkflowDefinitionRequest> body,
        [FromServices] IWorkflowDefinitionPublisher workflowDefinitionPublisher,
        [FromServices] VariableDefinitionMapper variableDefinitionMapper,
        [FromServices] IDistributedLockProvider distributedLockProvider,
        [FromServices] IWorkflowDefinitionLinker linker,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var model = request.Model;
        if (model == null)
        {
            return EngineResults.Errors("A workflow definition model is required.");
        }

        var definitionId = model.DefinitionId;
        var resourceName = $"{typeof(WorkflowDefinitionEndpoints).FullName}:{(!string.IsNullOrWhiteSpace(definitionId) ? definitionId : Guid.NewGuid().ToString())}";
        await using var handle = await distributedLockProvider.AcquireLockAsync(resourceName, TimeSpan.FromMinutes(1), cancellationToken);

        var draft = !string.IsNullOrWhiteSpace(definitionId)
            ? await workflowDefinitionPublisher.GetDraftAsync(definitionId, VersionOptions.Latest, cancellationToken)
            : null;

        if (draft == null)
        {
            draft = await workflowDefinitionPublisher.NewAsync(cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(definitionId))
            {
                draft.DefinitionId = definitionId;
            }
        }

        if (!await MayEditAsync(context, authorizationService, draft))
        {
            return EngineResults.Forbidden();
        }

        var serializerOptions = EngineResults.Options(context);
        draft.StringData = JsonSerializer.Serialize(model.Root ?? new Sequence(), serializerOptions);
        draft.MaterializerName = JsonWorkflowMaterializer.MaterializerName;
        draft.Name = model.Name?.Trim();
        draft.ToolVersion = model.ToolVersion;
        draft.Description = model.Description?.Trim();
        draft.CustomProperties = model.CustomProperties ?? new Dictionary<string, object>();
        draft.Variables = variableDefinitionMapper.Map(model.Variables).ToList();
        draft.Inputs = model.Inputs ?? new List<InputDefinition>();
        draft.Outputs = model.Outputs ?? new List<OutputDefinition>();
        draft.Outcomes = model.Outcomes ?? new List<string>();
        draft.Options = model.Options ?? new WorkflowOptions();

        PublishWorkflowDefinitionResult? result = null;
        if (request.Publish == true)
        {
            result = await workflowDefinitionPublisher.PublishAsync(draft, cancellationToken);
            if (!result.Succeeded)
            {
                return EngineResults.Errors(result.ValidationErrors.Select(error => error.Message));
            }
        }
        else
        {
            await workflowDefinitionPublisher.SaveDraftAsync(draft, cancellationToken);
        }

        var mappedDefinition = await linker.MapAsync(draft, cancellationToken);
        var affectedWorkflows = result?.AffectedWorkflows?.WorkflowDefinitions ?? [];
        return EngineResults.Json(context, new PublishResponse(mappedDefinition, false, affectedWorkflows.Count), serializerOptions);
    }

    private static async Task<IResult> PublishAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionPublisher workflowDefinitionPublisher,
        [FromServices] IWorkflowDefinitionLinker linker,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
        if (definition == null)
        {
            return EngineResults.NotFound();
        }

        if (!await MayEditAsync(context, authorizationService, definition))
        {
            return EngineResults.Forbidden();
        }

        var isPublished = definition.IsPublished;
        var result = !isPublished ? await workflowDefinitionPublisher.PublishAsync(definition, cancellationToken) : null;
        var mappedDefinition = await linker.MapAsync(definition, cancellationToken);
        return EngineResults.Json(context, new PublishResponse(mappedDefinition, isPublished, result?.AffectedWorkflows.WorkflowDefinitions.Count ?? 0));
    }

    private static async Task<IResult> RetractAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionPublisher workflowDefinitionPublisher,
        [FromServices] IWorkflowDefinitionLinker linker,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.LatestOrPublished }, cancellationToken);
        if (definition == null)
        {
            return EngineResults.NotFound();
        }

        if (!await MayEditAsync(context, authorizationService, definition))
        {
            return EngineResults.Forbidden();
        }

        if (!definition.IsPublished)
        {
            return EngineResults.Errors($"Workflow with id {definitionId} is not published");
        }

        await workflowDefinitionPublisher.RetractAsync(definition, cancellationToken);
        return EngineResults.Json(context, await linker.MapAsync(definition, cancellationToken));
    }

    private static async Task<IResult> UpdateReferencesAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromServices] IWorkflowReferenceUpdater workflowReferenceUpdater,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
        if (definition == null)
        {
            return EngineResults.NotFound();
        }

        if (!await MayEditAsync(context, authorizationService, definition))
        {
            return EngineResults.Forbidden();
        }

        var result = await workflowReferenceUpdater.UpdateWorkflowReferencesAsync(definition, cancellationToken);
        return EngineResults.Json(context, new UpdateReferencesResponse(result.UpdatedWorkflows.Select(w => w.Name ?? w.DefinitionId).ToList()));
    }

    /// <summary>Makes a new version from an old one; 201 with the new version's URL.</summary>
    private static async Task<IResult> RevertVersionAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromRoute] int version,
        [FromServices] IWorkflowDefinitionManager workflowDefinitionManager,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] LinkGenerator linkGenerator,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.SpecificVersion(version) }, cancellationToken);
        if (definition == null)
        {
            return EngineResults.NotFound();
        }

        if (!await MayEditAsync(context, authorizationService, definition))
        {
            return EngineResults.Forbidden();
        }

        var newDefinition = await workflowDefinitionManager.RevertVersionAsync(definitionId, version, cancellationToken);
        var summary = WorkflowDefinitionSummary.FromDefinition(newDefinition);
        context.Response.Headers.Location = linkGenerator.GetPathByName(context, GetByIdRouteName, new { id = newDefinition.Id });
        return EngineResults.Json(context, summary, StatusCodes.Status201Created);
    }

    private static async Task<IResult> DeleteAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromServices] IWorkflowDefinitionManager workflowDefinitionManager,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
        if (definition == null)
        {
            return EngineResults.NotFound();
        }

        if (!await MayEditAsync(context, authorizationService, definition))
        {
            return EngineResults.Forbidden();
        }

        var deleted = await workflowDefinitionManager.DeleteByDefinitionIdAsync(definitionId, cancellationToken);
        return deleted == 0 ? EngineResults.NotFound() : EngineResults.NoContent();
    }

    private static async Task<IResult> DeleteVersionAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [FromRoute] int version,
        [FromServices] IWorkflowDefinitionManager workflowDefinitionManager,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var definition = await store.FindAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.SpecificVersion(version) }, cancellationToken);
        if (definition == null)
        {
            return EngineResults.NotFound();
        }

        if (!await MayEditAsync(context, authorizationService, definition))
        {
            return EngineResults.Forbidden();
        }

        var deleted = await workflowDefinitionManager.DeleteVersionAsync(definition, cancellationToken);
        return deleted ? EngineResults.Ok() : EngineResults.NotFound();
    }

    private static async Task<IResult> DeleteVersionByIdAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromServices] IWorkflowDefinitionManager workflowDefinitionManager,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (!await MayEditAsync(context, authorizationService))
        {
            return EngineResults.Forbidden();
        }

        var deleted = await workflowDefinitionManager.DeleteByIdAsync(id, cancellationToken);
        return deleted ? EngineResults.NoContent() : EngineResults.NotFound();
    }

    private static async Task<IResult> DispatchAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        EngineBody<DispatchRequest> body,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        [FromServices] IWorkflowDispatcher workflowDispatcher,
        [FromServices] IIdentityGenerator identityGenerator,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(definitionId, request.VersionOptions ?? VersionOptions.Published, cancellationToken);
        if (workflowGraph == null)
        {
            return EngineResults.NotFound();
        }

        var instanceId = request.InstanceId ?? identityGenerator.GenerateId();
        var dispatchRequest = new DispatchWorkflowDefinitionRequest(workflowGraph.Workflow.Identity.Id)
        {
            Input = request.Input as IDictionary<string, object>,
            InstanceId = instanceId,
            CorrelationId = request.CorrelationId,
            TriggerActivityId = request.TriggerActivityId,
        };

        var result = await workflowDispatcher.DispatchAsync(dispatchRequest, new DispatchWorkflowOptions { Channel = request.Channel }, cancellationToken);
        return result.Succeeded ? EngineResults.Json(context, new DispatchResponse(instanceId)) : EngineResults.Errors(result.Fault!.Message);
    }

    private static async Task<IResult> BulkDispatchAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        EngineBody<BulkDispatchRequest> body,
        [FromServices] IWorkflowDefinitionService workflowDefinitionService,
        [FromServices] IWorkflowDispatcher workflowDispatcher,
        [FromServices] IIdentityGenerator identityGenerator,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(definitionId, request.VersionOptions ?? VersionOptions.Published, cancellationToken);
        if (workflowGraph == null)
        {
            return EngineResults.NotFound();
        }

        var instanceIds = new List<string>();
        for (var i = 0; i < request.Count; i++)
        {
            var instanceId = identityGenerator.GenerateId();
            var dispatchRequest = new DispatchWorkflowDefinitionRequest(workflowGraph.Workflow.Identity.Id)
            {
                Input = (IDictionary<string, object>?)request.Input,
                InstanceId = instanceId,
                TriggerActivityId = request.TriggerActivityId
            };
            await workflowDispatcher.DispatchAsync(dispatchRequest, null, cancellationToken);
            instanceIds.Add(instanceId);
        }

        return EngineResults.Json(context, new BulkDispatchResponse(instanceIds));
    }

    /// <summary>Re-indexes the definitions' triggers; 202 while a batch is still running.</summary>
    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        EngineBody<RefreshRequest> body,
        [FromServices] IWorkflowDefinitionsRefresher workflowDefinitionsRefresher,
        [FromServices] LinkGenerator linkGenerator,
        CancellationToken cancellationToken)
    {
        const int batchSize = 10;
        var result = await workflowDefinitionsRefresher.RefreshWorkflowDefinitionsAsync(new RefreshWorkflowDefinitionsRequest(body.Value.DefinitionIds, batchSize), cancellationToken);
        var response = new RefreshResponse(result.Refreshed, result.NotFound);
        if (result.Status == RefreshWorkflowDefinitionsStatus.Completed)
        {
            return EngineResults.Json(context, response);
        }

        context.Response.Headers.Location = linkGenerator.GetPathByName(context, RefreshRouteName);
        return EngineResults.Json(context, response, StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> ReloadAsync([FromServices] IWorkflowDefinitionsReloader workflowDefinitionsReloader, CancellationToken cancellationToken)
    {
        await workflowDefinitionsReloader.ReloadWorkflowDefinitionsAsync(cancellationToken);
        return EngineResults.Ok();
    }

    private static async Task<IResult> BulkDeleteAsync(
        HttpContext context,
        EngineBody<BulkDeleteByDefinitionIdsRequest> body,
        [FromServices] IWorkflowDefinitionManager workflowDefinitionManager,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (!await MayEditAsync(context, authorizationService))
        {
            return EngineResults.Forbidden();
        }

        var count = await workflowDefinitionManager.BulkDeleteByDefinitionIdsAsync(body.Value.DefinitionIds ?? [], cancellationToken);
        return EngineResults.Json(context, new BulkDeleteResponse(count));
    }

    private static async Task<IResult> BulkDeleteVersionsAsync(
        HttpContext context,
        EngineBody<BulkDeleteByIdsRequest> body,
        [FromServices] IWorkflowDefinitionManager workflowDefinitionManager,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (!await MayEditAsync(context, authorizationService))
        {
            return EngineResults.Forbidden();
        }

        var count = await workflowDefinitionManager.BulkDeleteByIdsAsync(body.Value.Ids ?? [], cancellationToken);
        return EngineResults.Json(context, new BulkDeleteResponse(count));
    }

    private static async Task<IResult> BulkPublishAsync(
        HttpContext context,
        EngineBody<BulkDeleteByDefinitionIdsRequest> body,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionPublisher workflowDefinitionPublisher,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (!await MayEditAsync(context, authorizationService))
        {
            return EngineResults.Forbidden();
        }

        var definitionIds = body.Value.DefinitionIds ?? [];
        var published = new List<string>();
        var notFound = new List<string>();
        var alreadyPublished = new List<string>();
        var skipped = new List<string>();
        var updatedConsumers = new List<string>();
        var definitions = (await store.FindManyAsync(new WorkflowDefinitionFilter { DefinitionIds = definitionIds, VersionOptions = VersionOptions.Latest }, cancellationToken: cancellationToken))
            .DistinctBy(x => x.DefinitionId)
            .ToDictionary(x => x.DefinitionId);

        foreach (var definitionId in definitionIds)
        {
            if (!definitions.TryGetValue(definitionId, out var definition))
            {
                notFound.Add(definitionId);
                continue;
            }

            if (definition.IsPublished)
            {
                alreadyPublished.Add(definitionId);
                continue;
            }

            if (definition.IsReadonly)
            {
                skipped.Add(definitionId);
                continue;
            }

            var result = await workflowDefinitionPublisher.PublishAsync(definition, cancellationToken);
            published.Add(definitionId);
            if (result.AffectedWorkflows.WorkflowDefinitions.Count > 0)
            {
                updatedConsumers.AddRange(result.AffectedWorkflows.WorkflowDefinitions.Select(x => x.DefinitionId));
            }
        }

        return EngineResults.Json(context, new BulkPublishResponse(published, alreadyPublished, notFound, skipped, updatedConsumers));
    }

    private static async Task<IResult> BulkRetractAsync(
        HttpContext context,
        EngineBody<BulkDeleteByDefinitionIdsRequest> body,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IWorkflowDefinitionPublisher workflowDefinitionPublisher,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (!await MayEditAsync(context, authorizationService))
        {
            return EngineResults.Forbidden();
        }

        var retracted = new List<string>();
        var notFound = new List<string>();
        var notPublished = new List<string>();
        var skipped = new List<string>();
        foreach (var definitionId in body.Value.DefinitionIds ?? [])
        {
            var definitions = (await store.FindManyAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = VersionOptions.LatestOrPublished }, cancellationToken: cancellationToken)).ToList();
            if (definitions.Count == 0)
            {
                notFound.Add(definitionId);
                continue;
            }

            var published = definitions.FirstOrDefault(d => d.IsPublished);
            if (published is null)
            {
                notPublished.Add(definitionId);
                continue;
            }

            if (published.IsReadonly)
            {
                skipped.Add(definitionId);
                continue;
            }

            await workflowDefinitionPublisher.RetractAsync(published, cancellationToken);
            retracted.Add(definitionId);
        }

        return EngineResults.Json(context, new BulkRetractResponse(retracted, notPublished, notFound, skipped));
    }
}
