using System.Text.Json;
using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Activities;
using Crest.Workflows.Api.Constants;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Api.Requirements;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Mappers;
using Crest.Workflows.Management.Materializers;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Models;
using JetBrains.Annotations;
using Medallion.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Post;

[PublicAPI]
internal class Post(
    IApiSerializer serializer,
    IWorkflowDefinitionPublisher workflowDefinitionPublisher,
    VariableDefinitionMapper variableDefinitionMapper,
    IDistributedLockProvider distributedLockProvider,
    IWorkflowDefinitionLinker linker,
    IAuthorizationService authorizationService)
    : CrestWorkflowsEndpoint<SaveWorkflowDefinitionRequest, LinkedWorkflowDefinitionModel>
{
    public override void Configure()
    {
        Post("/workflow-definitions");
        ConfigurePermissions("write:workflow-definitions");
    }

    public override async Task HandleAsync(SaveWorkflowDefinitionRequest request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        var definitionId = model.DefinitionId;
        var resourceName = $"{GetType().FullName}:{(!string.IsNullOrWhiteSpace(definitionId) ? definitionId : Guid.NewGuid().ToString())}";

        await using var handle = await distributedLockProvider.AcquireLockAsync(resourceName, TimeSpan.FromMinutes(1), cancellationToken);

        var draft = !string.IsNullOrWhiteSpace(definitionId)
            ? await workflowDefinitionPublisher.GetDraftAsync(definitionId, VersionOptions.Latest, cancellationToken)
            : null;

        var isNew = draft == null;

        // Create a new workflow in case no existing definition was found.
        if (isNew)
        {
            draft = await workflowDefinitionPublisher.NewAsync(cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(definitionId))
                draft.DefinitionId = definitionId;
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, new NotReadOnlyResource(draft), AuthorizationPolicies.NotReadOnlyPolicy);

        if (!authorizationResult.Succeeded)
        {
            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        // Update the draft with the received model.
        var root = model.Root ?? new Sequence();
        var serializerOptions = serializer.GetOptions();
        var stringData = JsonSerializer.Serialize(root, serializerOptions);
        var variables = variableDefinitionMapper.Map(model.Variables).ToList();
        var inputs = model.Inputs ?? new List<InputDefinition>();
        var outputs = model.Outputs ?? new List<OutputDefinition>();
        var outcomes = model.Outcomes ?? new List<string>();

        draft!.StringData = stringData;
        draft.MaterializerName = JsonWorkflowMaterializer.MaterializerName;
        draft.Name = model.Name?.Trim();
        draft.ToolVersion = model.ToolVersion;
        draft.Description = model.Description?.Trim();
        draft.CustomProperties = model.CustomProperties ?? new Dictionary<string, object>();
        draft.Variables = variables;
        draft.Inputs = inputs;
        draft.Outputs = outputs;
        draft.Outcomes = outcomes;
        draft.Options = model.Options ?? new WorkflowOptions();

        PublishWorkflowDefinitionResult? result = null;

        if (request.Publish == true)
        {
            result = await workflowDefinitionPublisher.PublishAsync(draft, cancellationToken);

            if (!result.Succeeded)
            {
                foreach (var validationError in result.ValidationErrors)
                    AddError(validationError.Message);

                await Send.ErrorsAsync(400, cancellationToken);
                return;
            }
        }
        else
        {
            await workflowDefinitionPublisher.SaveDraftAsync(draft, cancellationToken);
        }

        var mappedDefinition = await linker.MapAsync(draft, cancellationToken);
        var affectedWorkflows = result?.AffectedWorkflows?.WorkflowDefinitions ?? [];
        var response = new Response(mappedDefinition, false, affectedWorkflows.Count);
        await HttpContext.Response.WriteAsJsonAsync(response, serializerOptions, cancellationToken);
    }
}