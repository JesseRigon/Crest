using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Version;

[PublicAPI]
internal class ListVersions(IWorkflowDefinitionStore store) : CrestWorkflowsEndpointWithoutRequest
{
    public override void Configure()
    {
        Get("workflow-definitions/{definitionId}/versions");
        ConfigurePermissions("read:workflow-definitions");
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var definitionId = Route<string>("definitionId")!;
        
        var filter = new WorkflowDefinitionFilter
        {
            DefinitionId = definitionId,
            VersionOptions = VersionOptions.All
        };
        
        var orderBy = new WorkflowDefinitionOrder<int>(x => x.Version, OrderDirection.Descending);
        var definitions = await store.FindManyAsync(filter, orderBy, cancellationToken);
        
        if (!definitions.Any())
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.OkAsync(definitions, cancellationToken);
    }
}