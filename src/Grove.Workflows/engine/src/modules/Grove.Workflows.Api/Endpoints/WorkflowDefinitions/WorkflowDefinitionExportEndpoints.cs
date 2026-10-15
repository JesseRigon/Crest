using System.IO.Compression;
using System.Text.Json;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Mappers;
using Grove.Workflows.Management.Models;
using Humanizer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api.Endpoints.WorkflowDefinitions;

/// <summary>
/// Exports definitions as JSON downloads: one definition as a file, several (or one with its
/// consumers) as a zip. The selection comes from the query string or the body, by GET or POST.
/// </summary>
public static class WorkflowDefinitionExportEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bulk-actions/export/workflow-definitions", ExportManyAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapPost("/bulk-actions/export/workflow-definitions", ExportManyAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapGet("/workflow-definitions/{definitionId}/export", ExportOneAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/export", ExportOneAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowDefinitions);
    }

    private static Task<IResult> ExportManyAsync(
        HttpContext context,
        [AsParameters] ExportWorkflowDefinitionsQuery query,
        EngineBody<ExportWorkflowDefinitionsRequest> body,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IApiSerializer serializer,
        [FromServices] WorkflowDefinitionMapper mapper,
        [FromServices] IWorkflowReferenceGraphBuilder referenceGraphBuilder,
        CancellationToken cancellationToken) =>
        ExportAsync(query.DefinitionId ?? body.Value.DefinitionId, query, body.Value, store, serializer, mapper, referenceGraphBuilder, cancellationToken);

    private static Task<IResult> ExportOneAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        [AsParameters] ExportWorkflowDefinitionsQuery query,
        EngineBody<ExportWorkflowDefinitionsRequest> body,
        [FromServices] IWorkflowDefinitionStore store,
        [FromServices] IApiSerializer serializer,
        [FromServices] WorkflowDefinitionMapper mapper,
        [FromServices] IWorkflowReferenceGraphBuilder referenceGraphBuilder,
        CancellationToken cancellationToken) =>
        ExportAsync(definitionId, query, body.Value, store, serializer, mapper, referenceGraphBuilder, cancellationToken);

    private static async Task<IResult> ExportAsync(
        string? definitionId,
        ExportWorkflowDefinitionsQuery query,
        ExportWorkflowDefinitionsRequest request,
        IWorkflowDefinitionStore store,
        IApiSerializer serializer,
        WorkflowDefinitionMapper mapper,
        IWorkflowReferenceGraphBuilder referenceGraphBuilder,
        CancellationToken cancellationToken)
    {
        var ids = query.Ids is { Length: > 0 } ? query.Ids : request.Ids?.ToArray();
        var includeConsumers = query.IncludeConsumingWorkflows ?? request.IncludeConsumingWorkflows;
        var exporter = new Exporter(store, serializer, mapper, referenceGraphBuilder);

        if (definitionId != null)
        {
            return await exporter.ExportOneAsync(definitionId, query.VersionOptions ?? request.VersionOptions, includeConsumers, cancellationToken);
        }

        return ids != null ? await exporter.ExportManyAsync(ids, includeConsumers, cancellationToken) : EngineResults.NoContent();
    }

    private sealed class Exporter(IWorkflowDefinitionStore store, IApiSerializer serializer, WorkflowDefinitionMapper mapper, IWorkflowReferenceGraphBuilder referenceGraphBuilder)
    {
        public async Task<IResult> ExportManyAsync(ICollection<string> ids, bool includeConsumers, CancellationToken cancellationToken)
        {
            var definitions = (await store.FindManyAsync(new WorkflowDefinitionFilter { Ids = ids }, cancellationToken)).ToList();
            if (includeConsumers)
            {
                definitions = await IncludeConsumersAsync(definitions, cancellationToken);
            }

            return definitions.Count == 0 ? EngineResults.NoContent() : await ZipAsync(definitions, cancellationToken);
        }

        public async Task<IResult> ExportOneAsync(string definitionId, string? versionOptions, bool includeConsumers, CancellationToken cancellationToken)
        {
            var parsedVersionOptions = string.IsNullOrEmpty(versionOptions) ? VersionOptions.Latest : VersionOptions.FromString(versionOptions);
            var definition = (await store.FindManyAsync(new WorkflowDefinitionFilter { DefinitionId = definitionId, VersionOptions = parsedVersionOptions }, cancellationToken)).FirstOrDefault();
            if (definition == null)
            {
                return EngineResults.NotFound();
            }

            if (includeConsumers)
            {
                return await ZipAsync(await IncludeConsumersAsync([definition], cancellationToken), cancellationToken);
            }

            var model = await mapper.MapAsync(definition, cancellationToken);
            return EngineResults.File(await SerializeAsync(model, cancellationToken), FileName(model));
        }

        // Consumers are resolved at Latest, whatever version the initial definitions came in.
        private async Task<List<WorkflowDefinition>> IncludeConsumersAsync(List<WorkflowDefinition> definitions, CancellationToken cancellationToken)
        {
            var initialDefinitionIds = definitions.Select(d => d.DefinitionId).ToList();
            var graph = await referenceGraphBuilder.BuildGraphAsync(initialDefinitionIds, cancellationToken);
            var newDefinitionIds = graph.ConsumerDefinitionIds.Except(initialDefinitionIds).ToList();
            if (newDefinitionIds.Count > 0)
            {
                var consumers = await store.FindManyAsync(new WorkflowDefinitionFilter { DefinitionIds = newDefinitionIds.ToArray(), VersionOptions = VersionOptions.Latest }, cancellationToken);
                definitions = definitions.Concat(consumers).ToList();
            }

            return definitions;
        }

        private async Task<IResult> ZipAsync(List<WorkflowDefinition> definitions, CancellationToken cancellationToken)
        {
            var zipStream = new MemoryStream();

            // A fixed entry timestamp (the ZIP format's minimum) keeps identical exports byte-identical.
            var zipEpoch = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            await using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var definition in definitions.OrderBy(d => d.DefinitionId))
                {
                    var model = await mapper.MapAsync(definition, cancellationToken);
                    var binaryJson = await SerializeAsync(model, cancellationToken);
                    var entry = zipArchive.CreateEntry(FileName(model), CompressionLevel.Optimal);
                    entry.LastWriteTime = zipEpoch;
                    await using var entryStream = await entry.OpenAsync(cancellationToken);
                    await entryStream.WriteAsync(binaryJson, cancellationToken);
                }
            }

            return EngineResults.File(zipStream.ToArray(), "workflow-definitions.zip");
        }

        private static string FileName(WorkflowDefinitionModel definition)
        {
            var workflowName = !string.IsNullOrWhiteSpace(definition.Name) ? definition.Name!.Trim() : definition.DefinitionId;
            return $"workflow-definition-{workflowName.Underscore().Dasherize().ToLowerInvariant()}-{definition.DefinitionId}.json";
        }

        private async Task<byte[]> SerializeAsync(WorkflowDefinitionModel model, CancellationToken cancellationToken)
        {
            var document = JsonSerializer.SerializeToDocument(model, serializer.GetOptions());
            using var output = new MemoryStream();
            await using var writer = new Utf8JsonWriter(output);
            writer.WriteStartObject();
            writer.WriteString("$schema", "https://github.com/JesseRigon/Grove/blob/main/docs/workflows.md");
            foreach (var property in document.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
            await writer.FlushAsync(cancellationToken);
            return output.ToArray();
        }
    }
}

/// <summary>The export selection by query string; a definition id, or version ids.</summary>
public class ExportWorkflowDefinitionsQuery
{
    [FromQuery] public string? DefinitionId { get; set; }

    [FromQuery] public string? VersionOptions { get; set; }

    [FromQuery] public string[]? Ids { get; set; }

    [FromQuery] public bool? IncludeConsumingWorkflows { get; set; }
}

/// <summary>The export selection by body.</summary>
public class ExportWorkflowDefinitionsRequest
{
    public string? DefinitionId { get; set; }

    public string? VersionOptions { get; set; }

    public ICollection<string>? Ids { get; set; }

    public bool IncludeConsumingWorkflows { get; set; }
}
