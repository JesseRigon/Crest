using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Api.Constants;
using Crest.Workflows.Api.Requirements;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Management.Models;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Version;

[PublicAPI]
internal class RevertVersion(IWorkflowDefinitionManager workflowDefinitionManager, IAuthorizationService authorizationService, IWorkflowDefinitionStore store)
    : CrestWorkflowsEndpointWithoutRequest
{
    public override void Configure()
    {
        Post("workflow-definitions/{definitionId}/revert/{version}");
        ConfigurePermissions("publish:workflow-definitions");
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var definitionId = Route<string>("definitionId")!;
        var version = Route<int>("version");

        var filter = new WorkflowDefinitionFilter
        {
            DefinitionId = definitionId,
            VersionOptions = VersionOptions.SpecificVersion(version)
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

        var newDefinition = await workflowDefinitionManager.RevertVersionAsync(definitionId, version, cancellationToken);
        var newDefinitionSummary = WorkflowDefinitionSummary.FromDefinition(newDefinition);

        await Send.CreatedAtAsync("GetWorkflowDefinitionById", new
        {
            id = newDefinition.Id
        }, newDefinitionSummary, cancellation: cancellationToken);
    }
}