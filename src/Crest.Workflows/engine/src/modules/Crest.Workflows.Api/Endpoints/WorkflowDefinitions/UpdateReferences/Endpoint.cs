using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Api.Constants;
using Crest.Workflows.Api.Requirements;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Contracts;
using Crest.Workflows.Management.Filters;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.UpdateReferences;

[PublicAPI]
internal class UpdateReferences(IWorkflowReferenceUpdater workflowReferenceUpdater, IWorkflowDefinitionStore store, IAuthorizationService authorizationService)
    : CrestWorkflowsEndpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/workflow-definitions/{definitionId}/update-references");
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

        var result = await workflowReferenceUpdater.UpdateWorkflowReferencesAsync(definition, cancellationToken);
        var affectedWorkflows = result.UpdatedWorkflows;
        var response = new Response(affectedWorkflows.Select(w => w.Name ?? w.DefinitionId));
        await Send.OkAsync(response, cancellationToken);
    }
}