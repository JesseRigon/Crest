using Grove.Api;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/triggers</c>: the stored triggers of this tenant, and a re-index for
/// one definition. Grove.Workflows indexes triggers when a definition is published; this is how
/// an operator (or the test suite) sees what a published flow actually listens for, without
/// reading the database.
/// </summary>
public sealed class WorkflowTriggersApi : ApiContributor
{
    public override string Name => "WorkflowTriggers";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("workflows/triggers");
        routes.MapGet("", (HttpContext http, [FromQuery] string? definitionId, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowTriggersHandlers>(http).ListAsync(definitionId, cancellationToken));
        routes.MapPost("{definitionId}/reindex", (HttpContext http, [FromRoute] string definitionId, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowTriggersHandlers>(http).ReindexAsync(definitionId, cancellationToken));
    }
}

public sealed class WorkflowTriggersHandlers(
    IAuthorizationService authorizationService,
    ITriggerStore triggerStore,
    ITriggerIndexer triggerIndexer,
    IWorkflowDefinitionStore definitionStore) : ApiHandlers
{
    public async Task<IResult> ListAsync(string? definitionId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        var filter = new TriggerFilter { WorkflowDefinitionId = string.IsNullOrWhiteSpace(definitionId) ? null : definitionId };
        var triggers = await triggerStore.FindManyAsync(filter, cancellationToken);

        return Ok(triggers.Select(trigger => new WorkflowTriggerModel(
            trigger.Id,
            trigger.WorkflowDefinitionId,
            trigger.WorkflowDefinitionVersionId,
            trigger.ActivityId,
            trigger.Name,
            trigger.Hash)).ToList());
    }

    public async Task<IResult> ReindexAsync(string definitionId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.PublishWorkflows))
        {
            return Forbid();
        }

        var definition = await definitionStore.FindAsync(new WorkflowDefinitionFilter
        {
            DefinitionId = definitionId,
            VersionOptions = VersionOptions.Published,
        }, cancellationToken);

        if (definition is null)
        {
            return NotFound();
        }

        var result = await triggerIndexer.IndexTriggersAsync(definition, cancellationToken);
        return Ok(new WorkflowTriggerIndexResult(result.AddedTriggers.Count, result.RemovedTriggers.Count, result.UnchangedTriggers.Count));
    }
}

public sealed record WorkflowTriggerModel(string Id, string DefinitionId, string DefinitionVersionId, string ActivityId, string Name, string? Hash);
public sealed record WorkflowTriggerIndexResult(int Added, int Removed, int Unchanged);
