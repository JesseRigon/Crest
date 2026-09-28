using Elsa.Common.Models;
using Elsa.Workflows.Management;
using Elsa.Workflows.Management.Filters;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrchardCore.Crest.Workflows.Controllers;

/// <summary>
/// The stored triggers of this tenant, and a re-index for one definition. Elsa indexes
/// triggers when a definition is published; this is how an operator (or the test suite)
/// sees what a published flow actually listens for, without reading the database.
/// Crest conventions: cookie + antiforgery, Orchard permission, tenant-prefixed route.
/// </summary>
[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/workflows/triggers")]
public sealed class WorkflowTriggersController(
    IAuthorizationService authorizationService,
    ITriggerStore triggerStore,
    ITriggerIndexer triggerIndexer,
    IWorkflowDefinitionStore definitionStore) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string? definitionId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ManageWorkflows))
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
            trigger.Hash)));
    }

    [HttpPost("{definitionId}/reindex")]
    public async Task<IActionResult> ReindexAsync(string definitionId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ManageWorkflows))
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
