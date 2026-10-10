using Crest.Workflows.Abstractions;
using Crest.Workflows.Api.Constants;
using Crest.Workflows.Api.Requirements;
using Crest.Workflows.Management;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.BulkDeleteVersions;

[PublicAPI]
internal class BulkDeleteVersions(IWorkflowDefinitionManager workflowDefinitionManager, IAuthorizationService authorizationService)
    : CrestWorkflowsEndpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/bulk-actions/delete/workflow-definitions/by-id");
        ConfigurePermissions("delete:workflow-definitions");
    }

    public override async Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(User, new NotReadOnlyResource(), AuthorizationPolicies.NotReadOnlyPolicy);

        if (!authorizationResult.Succeeded)
        {
            await Send.ForbiddenAsync(cancellationToken);
            return null!;
        }

        var count = await workflowDefinitionManager.BulkDeleteByIdsAsync(request.Ids, cancellationToken);
        return new Response(count);
    }
}