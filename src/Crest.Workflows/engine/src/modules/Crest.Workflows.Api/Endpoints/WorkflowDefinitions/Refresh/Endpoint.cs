using Crest.Workflows.Abstractions;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Refresh;

[PublicAPI]
internal class Refresh(IWorkflowDefinitionsRefresher workflowDefinitionsRefresher) : CrestWorkflowsEndpoint<Request>
{
    private const int BatchSize = 10;

    public override void Configure()
    {
        Post("/actions/workflow-definitions/refresh");
        ConfigurePermissions("actions:workflow-definitions:refresh");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var result = await RefreshWorkflowDefinitionsAsync(request.DefinitionIds, cancellationToken);
        if (result.Status == RefreshWorkflowDefinitionsStatus.Completed)
        {
            await Send.OkAsync(new Response(result.Refreshed, result.NotFound), cancellationToken);
        }
        else
        {
            await Send.AcceptedAtAsync<Refresh>(responseBody: new Response(result.Refreshed, result.NotFound), cancellation: cancellationToken);
        }
    }

    private async Task<RefreshWorkflowDefinitionsResponse> RefreshWorkflowDefinitionsAsync(ICollection<string>? definitionIds, CancellationToken cancellationToken)
    {
        var request = new RefreshWorkflowDefinitionsRequest(definitionIds, BatchSize);
        return await workflowDefinitionsRefresher.RefreshWorkflowDefinitionsAsync(request, cancellationToken);
    }
}