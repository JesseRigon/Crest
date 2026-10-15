using System.IO.Compression;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api.Endpoints.WorkflowDefinitions;

/// <summary>Imports definitions: one as a JSON body, or any number as uploaded JSON and zip files.</summary>
public static class WorkflowDefinitionImportEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/workflow-definitions/import", ImportAsync).RequireAuthorization(EngineApiPolicies.WriteWorkflowDefinitions);
        routes.MapPut("/workflow-definitions/import", ImportAsync).RequireAuthorization(EngineApiPolicies.WriteWorkflowDefinitions);
        routes.MapPost("/workflow-definitions/{definitionId}/import", ImportByDefinitionIdAsync).RequireAuthorization(EngineApiPolicies.WriteWorkflowDefinitions);
        routes.MapPut("/workflow-definitions/{definitionId}/import", ImportByDefinitionIdAsync).RequireAuthorization(EngineApiPolicies.WriteWorkflowDefinitions);

        // Form binding; the registry's antiforgery filter covers the group, the framework's own is off.
        routes.MapPost("/workflow-definitions/import-files", ImportFilesAsync).DisableAntiforgery().RequireAuthorization(EngineApiPolicies.WriteWorkflowDefinitions);
    }

    private static Task<IResult> ImportAsync(
        HttpContext context,
        EngineBody<WorkflowDefinitionModel> body,
        [FromServices] IWorkflowDefinitionImporter importer,
        [FromServices] IWorkflowDefinitionLinker linker,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] LinkGenerator linkGenerator,
        CancellationToken cancellationToken) =>
        ImportModelAsync(context, body.Value, importer, linker, authorizationService, linkGenerator, cancellationToken);

    private static Task<IResult> ImportByDefinitionIdAsync(
        HttpContext context,
        [FromRoute] string definitionId,
        EngineBody<WorkflowDefinitionModel> body,
        [FromServices] IWorkflowDefinitionImporter importer,
        [FromServices] IWorkflowDefinitionLinker linker,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] LinkGenerator linkGenerator,
        CancellationToken cancellationToken)
    {
        body.Value.DefinitionId = definitionId;
        return ImportModelAsync(context, body.Value, importer, linker, authorizationService, linkGenerator, cancellationToken);
    }

    private static async Task<IResult> ImportModelAsync(
        HttpContext context,
        WorkflowDefinitionModel model,
        IWorkflowDefinitionImporter importer,
        IWorkflowDefinitionLinker linker,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        CancellationToken cancellationToken)
    {
        var isNew = string.IsNullOrWhiteSpace(model.DefinitionId);
        var result = await importer.ImportAsync(new SaveWorkflowDefinitionRequest { Model = model, Publish = false }, cancellationToken);
        if (!await WorkflowDefinitionEndpoints.MayEditAsync(context, authorizationService, result.WorkflowDefinition))
        {
            return EngineResults.Forbidden();
        }

        if (!result.Succeeded)
        {
            return EngineResults.Errors(result.ValidationErrors.Select(error => error.Message));
        }

        var updatedModel = await linker.MapAsync(result.WorkflowDefinition, cancellationToken);
        if (!isNew)
        {
            return EngineResults.Json(context, updatedModel);
        }

        context.Response.Headers.Location = linkGenerator.GetPathByName(context, WorkflowDefinitionEndpoints.GetByIdRouteName, new { id = result.WorkflowDefinition.Id });
        return EngineResults.Json(context, updatedModel, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ImportFilesAsync(
        HttpContext context,
        [FromForm] IFormFileCollection files,
        [FromServices] IWorkflowDefinitionImporter importer,
        [FromServices] IApiSerializer apiSerializer,
        [FromServices] IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (!await WorkflowDefinitionEndpoints.MayEditAsync(context, authorizationService))
        {
            return EngineResults.Forbidden();
        }

        if (files.Count == 0)
        {
            return EngineResults.Ok();
        }

        var count = 0;
        var errors = new List<string>();
        foreach (var file in files)
        {
            var fileStream = file.OpenReadStream();
            if (file.ContentType == "application/json")
            {
                await ImportStreamAsync(fileStream);
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

                await ImportStreamAsync(entry.Open());
                count++;
            }
        }

        return errors.Count > 0 ? EngineResults.Errors(errors) : EngineResults.Json(context, new { Count = count });

        async Task ImportStreamAsync(Stream jsonStream)
        {
            var json = await new StreamReader(jsonStream).ReadToEndAsync(cancellationToken);
            var model = apiSerializer.Deserialize<WorkflowDefinitionModel>(json);
            var result = await importer.ImportAsync(new SaveWorkflowDefinitionRequest { Model = model, Publish = false }, cancellationToken);
            if (!result.Succeeded)
            {
                errors.AddRange(result.ValidationErrors.Select(error => error.Message));
            }
        }
    }
}
