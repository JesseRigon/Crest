using Crest.Workflows.Abstractions;
using Crest.Workflows.Extensions;
using Crest.Workflows.Management;
using Crest.Workflows.Serialization.Converters;
using Crest.Workflows.Serialization.Helpers;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Graph;

[PublicAPI]
internal class Graph(IWorkflowDefinitionService workflowDefinitionService, IApiSerializer apiSerializer, ActivityWriter activityWriter) : WorkflowsEndpoint<Request>
{
    public override void Configure()
    {
        Get("/workflow-definitions/subgraph/{id}");
        ConfigurePermissions("read:workflow-definitions");
    }

    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var workflowGraph = await workflowDefinitionService.FindWorkflowGraphAsync(request.Id, cancellationToken);

        if (workflowGraph == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        var parentNode = workflowGraph.NodeIdLookup.TryGetValue(request.ParentNodeId, out var node) ? node : workflowGraph.Root;
        var serializerOptions = apiSerializer.GetOptions().Clone();
        serializerOptions.Converters.Add(new RootActivityNodeConverter(activityWriter));
        await HttpContext.Response.WriteAsJsonAsync(parentNode, serializerOptions, cancellationToken);
    }
}