using Crest.Workflows.Abstractions;
using Crest.Workflows.Runtime;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Api.Endpoints.WorkflowInstances.Cancel;

[PublicAPI]
internal class Cancel(IWorkflowCancellationService workflowCancellationService)
    : CrestWorkflowsEndpoint<Request>
{
    public override void Configure()
    {
        Post("/cancel/workflow-instances/{id}");
        ConfigurePermissions("cancel:workflow-instances");
        //Allows for post with empty body
        Description(x => x.Accepts<Request>("*/*"), clearDefaults: true);
    }
    
    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        await workflowCancellationService.CancelWorkflowAsync(request.Id, cancellationToken);
        
        await Send.OkAsync(cancellation: cancellationToken);
    }
}