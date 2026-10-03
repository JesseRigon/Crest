using Crest.Workflows.Abstractions;
using Crest.Workflows.Models;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.GetManyById;

[PublicAPI]
internal class GetManyById(IWorkflowDefinitionStore store, IWorkflowDefinitionLinker linker) : CrestWorkflowsEndpoint<Request>
{
    public override void Configure()
    {
        Get("/workflow-definitions/many-by-id");
        ConfigurePermissions("read:workflow-definitions");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var filter = new WorkflowDefinitionFilter
        {
            Ids = request.Ids
        };

        var definitions = (await store.FindManyAsync(filter, cancellationToken)).ToList();
        var models = await linker.MapAsync(definitions, cancellationToken);
        var response = new ListResponse<LinkedWorkflowDefinitionModel>(models);
        await Send.OkAsync(response, cancellationToken);
    }
}