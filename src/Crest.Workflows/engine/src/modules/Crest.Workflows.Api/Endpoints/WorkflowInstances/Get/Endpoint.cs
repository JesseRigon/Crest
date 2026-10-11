using Crest.Workflows.Abstractions;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.WorkflowInstances.Get;

[PublicAPI]
internal class Get(IWorkflowInstanceStore store) : WorkflowsEndpoint<Request, WorkflowInstanceModel, WorkflowInstanceMapper>
{
    public override void Configure()
    {
        Get("/workflow-instances/{id}");
        ConfigurePermissions("read:workflow-instances");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var filter = new WorkflowInstanceFilter { Id = request.Id };
        var workflowInstance = await store.FindAsync(filter, cancellationToken);

        if (workflowInstance == null)
            await Send.NotFoundAsync(cancellationToken);
        else
            await Send.OkAsync(Map.FromEntity(workflowInstance), cancellationToken);
    }
}