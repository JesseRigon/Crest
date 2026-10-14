using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Crest.Workflows.Api.Endpoints.WorkflowInstances;

/// <summary>
/// Exports instances as JSON downloads (one as a file, several as a zip; state, and on request
/// bookmarks and the execution logs) and imports them back from uploaded JSON and zip files.
/// </summary>
public static class WorkflowInstanceExportEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bulk-actions/export/workflow-instances", ExportManyAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapPost("/bulk-actions/export/workflow-instances", ExportManyAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapGet("/workflow-instances/{id}/export", ExportOneAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapPost("/workflow-instances/{id}/export", ExportOneAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);

        // Form binding; the registry's antiforgery filter covers the group, the framework's own is off.
        routes.MapPost("/bulk-actions/import/workflow-instances", ImportAsync).DisableAntiforgery().RequireAuthorization(EngineApiPolicies.WriteWorkflowInstances);
    }

    private static Task<IResult> ExportManyAsync(
        [AsParameters] ExportWorkflowInstancesQuery query,
        EngineBody<ExportWorkflowInstancesRequest> body,
        [FromServices] InstanceExporter exporter,
        CancellationToken cancellationToken) =>
        ExportAsync(query.Id ?? body.Value.Id, query, body.Value, exporter, cancellationToken);

    private static Task<IResult> ExportOneAsync(
        [FromRoute] string id,
        [AsParameters] ExportWorkflowInstancesQuery query,
        EngineBody<ExportWorkflowInstancesRequest> body,
        [FromServices] InstanceExporter exporter,
        CancellationToken cancellationToken) =>
        ExportAsync(id, query, body.Value, exporter, cancellationToken);

    private static Task<IResult> ExportAsync(string? id, ExportWorkflowInstancesQuery query, ExportWorkflowInstancesRequest request, InstanceExporter exporter, CancellationToken cancellationToken)
    {
        var ids = query.Ids is { Length: > 0 } ? query.Ids : request.Ids?.ToArray() ?? [];
        var options = new ExportOptions(
            query.IncludeBookmarks ?? request.IncludeBookmarks,
            query.IncludeActivityExecutionLog ?? request.IncludeActivityExecutionLog,
            query.IncludeWorkflowExecutionLog ?? request.IncludeWorkflowExecutionLog);

        return id != null || ids.Length == 1
            ? exporter.ExportOneAsync(id ?? ids[0], options, cancellationToken)
            : exporter.ExportManyAsync(ids, options, cancellationToken);
    }

    private static async Task<IResult> ImportAsync(
        HttpContext context,
        [FromForm] IFormFileCollection files,
        [FromServices] InstanceImporter importer,
        CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var file in files)
        {
            var fileStream = file.OpenReadStream();
            if (file.ContentType == "application/json")
            {
                await importer.ImportAsync(fileStream, cancellationToken);
                count++;
                continue;
            }

            var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            foreach (var entry in zipArchive.Entries)
            {
                if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await importer.ImportAsync(entry.Open(), cancellationToken);
                count++;
            }
        }

        return EngineResults.Json(context, new ImportWorkflowInstancesResponse { Imported = count });
    }

    internal sealed record ExportOptions(bool IncludeBookmarks, bool IncludeActivityExecutionLog, bool IncludeWorkflowExecutionLog);

    /// <summary>Builds the export model of an instance from its state, bookmarks and logs.</summary>
    public sealed class InstanceExporter(
        IWorkflowInstanceStore workflowInstanceStore,
        IActivityExecutionStore activityExecutionStore,
        IWorkflowExecutionLogStore workflowExecutionLogStore,
        IBookmarkStore bookmarkStore,
        IWorkflowStateSerializer workflowStateSerializer,
        IPayloadSerializer payloadSerializer,
        ISafeSerializer safeSerializer,
        IWorkflowInstanceExportNameProvider exportNameProvider)
    {
        internal async Task<IResult> ExportManyAsync(ICollection<string> ids, ExportOptions options, CancellationToken cancellationToken)
        {
            var instances = (await workflowInstanceStore.FindManyAsync(new WorkflowInstanceFilter { Ids = ids }, cancellationToken: cancellationToken)).ToList();
            if (instances.Count == 0)
            {
                return EngineResults.NoContent();
            }

            var zipStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var instance in instances)
                {
                    var model = await CreateModelAsync(instance, options, cancellationToken);
                    var fileName = await exportNameProvider.GetFileNameAsync(instance, model, cancellationToken);
                    var entry = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(model), cancellationToken);
                }
            }

            return EngineResults.File(zipStream.ToArray(), "workflow-instances.zip");
        }

        internal async Task<IResult> ExportOneAsync(string id, ExportOptions options, CancellationToken cancellationToken)
        {
            var instance = (await workflowInstanceStore.FindManyAsync(new WorkflowInstanceFilter { Id = id }, cancellationToken: cancellationToken)).FirstOrDefault();
            if (instance == null)
            {
                return EngineResults.NotFound();
            }

            var model = await CreateModelAsync(instance, options, cancellationToken);
            var fileName = await exportNameProvider.GetFileNameAsync(instance, model, cancellationToken);
            return EngineResults.File(JsonSerializer.SerializeToUtf8Bytes(model), fileName);
        }

        private async Task<ExportedWorkflowState> CreateModelAsync(WorkflowInstance instance, ExportOptions options, CancellationToken cancellationToken)
        {
            var workflowState = instance.WorkflowState;
            var workflowStateElement = workflowStateSerializer.SerializeToElement(workflowState);
            var bookmarksElement = options.IncludeBookmarks ? SerializeBookmarks(await bookmarkStore.FindManyAsync(new BookmarkFilter { WorkflowInstanceId = workflowState.Id }, cancellationToken)) : default(JsonElement?);
            var activityLogElement = options.IncludeActivityExecutionLog ? safeSerializer.SerializeToElement(await LoadActivityExecutionLogAsync(workflowState.Id, cancellationToken)) : default(JsonElement?);
            var workflowLogElement = options.IncludeWorkflowExecutionLog ? safeSerializer.SerializeToElement(await LoadWorkflowExecutionLogAsync(workflowState.Id, cancellationToken)) : default(JsonElement?);
            return new ExportedWorkflowState(workflowStateElement, bookmarksElement, activityLogElement, workflowLogElement);
        }

        private JsonElement SerializeBookmarks(IEnumerable<StoredBookmark> bookmarks)
        {
            var nodes = bookmarks.Select(x => new JsonObject
            {
                ["id"] = x.Id,
                ["activityTypeName"] = x.Name,
                ["workflowInstanceId"] = x.WorkflowInstanceId,
                ["activityInstanceId"] = x.ActivityInstanceId,
                ["hash"] = x.Hash,
                ["correlationId"] = x.CorrelationId,
                ["createdAt"] = x.CreatedAt,
                ["payload"] = JsonObject.Create(payloadSerializer.SerializeToElement(x.Payload!)),
                ["metadata"] = JsonObject.Create(payloadSerializer.SerializeToElement(x.Metadata!))
            }).Cast<JsonNode>().ToArray();

            return JsonSerializer.SerializeToElement(new JsonArray(nodes));
        }

        private async Task<IEnumerable<ActivityExecutionRecord>> LoadActivityExecutionLogAsync(string workflowInstanceId, CancellationToken cancellationToken)
        {
            var order = new ActivityExecutionRecordOrder<DateTimeOffset>(x => x.StartedAt, OrderDirection.Ascending);
            return await activityExecutionStore.FindManyAsync(new ActivityExecutionRecordFilter { WorkflowInstanceId = workflowInstanceId }, order, cancellationToken);
        }

        private async Task<IEnumerable<WorkflowExecutionLogRecord>> LoadWorkflowExecutionLogAsync(string workflowInstanceId, CancellationToken cancellationToken)
        {
            var order = new WorkflowExecutionLogRecordOrder<DateTimeOffset>(x => x.Timestamp, OrderDirection.Ascending);
            var page = await workflowExecutionLogStore.FindManyAsync(new WorkflowExecutionLogRecordFilter { WorkflowInstanceId = workflowInstanceId }, PageArgs.All, order, cancellationToken);
            return page.Items;
        }
    }

    /// <summary>Saves an exported instance back: its state, then bookmarks and logs when the export carried them.</summary>
    public sealed class InstanceImporter(
        IWorkflowInstanceManager workflowInstanceManager,
        IActivityExecutionStore activityExecutionStore,
        IWorkflowExecutionLogStore workflowExecutionLogStore,
        IBookmarkStore bookmarkStore,
        IWorkflowStateSerializer workflowStateSerializer,
        IPayloadSerializer payloadSerializer,
        ISafeSerializer safeSerializer)
    {
        public async Task ImportAsync(Stream jsonStream, CancellationToken cancellationToken)
        {
            var json = await new StreamReader(jsonStream).ReadToEndAsync(cancellationToken);
            var model = JsonSerializer.Deserialize<ExportedWorkflowState>(json)!;
            var workflowState = workflowStateSerializer.Deserialize(model.WorkflowState);
            await workflowInstanceManager.SaveAsync(workflowState, cancellationToken);

            if (model.Bookmarks != null)
            {
                var bookmarks = model.Bookmarks.Value.EnumerateArray().Select(x => new StoredBookmark
                {
                    Id = x.GetProperty("id").GetString()!,
                    Name = x.GetProperty("activityTypeName").GetString()!,
                    Hash = x.GetProperty("hash").GetString()!,
                    WorkflowInstanceId = x.GetProperty("workflowInstanceId").GetString()!,
                    CreatedAt = x.GetProperty("createdAt").GetDateTimeOffset(),
                    ActivityInstanceId = x.GetProperty("activityInstanceId").GetString(),
                    CorrelationId = x.GetProperty("correlationId").GetString(),
                    Payload = payloadSerializer.Deserialize<object>(x.GetProperty("payload")),
                    Metadata = payloadSerializer.Deserialize<IDictionary<string, string>>(x.GetProperty("metadata"))
                }).ToList();

                await bookmarkStore.SaveManyAsync(bookmarks, cancellationToken);
            }

            if (model.ActivityExecutionRecords != null)
            {
                var records = safeSerializer.Deserialize<ICollection<ActivityExecutionRecord>>(model.ActivityExecutionRecords.Value);
                await activityExecutionStore.SaveManyAsync(records, cancellationToken);
            }

            if (model.WorkflowExecutionLogRecords != null)
            {
                var records = safeSerializer.Deserialize<ICollection<WorkflowExecutionLogRecord>>(model.WorkflowExecutionLogRecords.Value);
                await workflowExecutionLogStore.SaveManyAsync(records, cancellationToken);
            }
        }
    }
}

public class ExportWorkflowInstancesQuery
{
    [FromQuery] public string? Id { get; set; }

    [FromQuery] public string[]? Ids { get; set; }

    [FromQuery] public bool? IncludeBookmarks { get; set; }

    [FromQuery] public bool? IncludeActivityExecutionLog { get; set; }

    [FromQuery] public bool? IncludeWorkflowExecutionLog { get; set; }
}

public class ExportWorkflowInstancesRequest
{
    public string? Id { get; set; }

    public ICollection<string>? Ids { get; set; }

    public bool IncludeBookmarks { get; set; }

    public bool IncludeActivityExecutionLog { get; set; }

    public bool IncludeWorkflowExecutionLog { get; set; }
}

public class ImportWorkflowInstancesResponse
{
    public int Imported { get; set; }
}
