using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Api.Constants;
using Crest.Workflows.Api.Requirements;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Publish;

[PublicAPI]
internal class Publish(IWorkflowDefinitionStore store, IWorkflowDefinitionPublisher workflowDefinitionPublisher, IWorkflowDefinitionLinker linker, IAuthorizationService authorizationService)
    : CrestWorkflowsEndpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/workflow-definitions/{definitionId}/publish");
        ConfigurePermissions("publish:workflow-definitions");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var filter = new WorkflowDefinitionFilter
        {
            DefinitionId = request.DefinitionId,
            VersionOptions = VersionOptions.Latest
        };

        var definition = await store.FindAsync(filter, cancellationToken);

        if (definition == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, new NotReadOnlyResource(definition), AuthorizationPolicies.NotReadOnlyPolicy);

        if (!authorizationResult.Succeeded)
        {
            await Send.ForbiddenAsync(cancellationToken);
            return;
        }

        var isPublished = definition.IsPublished;
        var result = !isPublished ? await workflowDefinitionPublisher.PublishAsync(definition, cancellationToken) : null;
        var mappedDefinition = await linker.MapAsync(definition, cancellationToken);
        var response = new Response(mappedDefinition, isPublished, result?.AffectedWorkflows.WorkflowDefinitions.Count ?? 0);
        await Send.OkAsync(response, cancellationToken);
    }
}