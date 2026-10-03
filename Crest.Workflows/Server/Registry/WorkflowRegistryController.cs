using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Workflows.Registry;

/// <summary>What the registries contributed, and where each shipped flow landed in this tenant.</summary>
[ApiController, AutoValidateAntiforgeryToken, Route(WorkflowsConstants.Routes.RegistryApi)]
public sealed class WorkflowRegistryController(IAuthorizationService authorizationService, WorkflowRegistryCatalog catalog, WorkflowFlowLookup lookup, WorkflowFlowImporter importer, IActivityRegistry activityRegistry) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        var flows = new List<WorkflowFlowModel>();
        foreach (var flow in catalog.Flows)
        {
            var found = await lookup.FindAsync(flow.Key, cancellationToken);
            int? installedVersion = found is null ? null : found.Ownership.FlowVersion ?? 1;
            flows.Add(new WorkflowFlowModel(
                flow.Key,
                flow.DisplayName,
                flow.Object,
                found?.DefinitionId,
                found?.Published ?? false,
                WorkflowOwnership.Normalize(flow.Ownership),
                flow.Version,
                installedVersion,
                found?.Ownership.Forked ?? false,
                Outdated: installedVersion is not null && installedVersion < flow.Version));
        }

        // Each activity's declared field dependencies, from its engine descriptor (the class
        // attributes, placed there by the descriptor modifier), so the palette and a "what this
        // flow needs" panel can show them.
        var activities = catalog.Activities.Select(a => a.FieldDependencies is not null ? a : a with
        {
            FieldDependencies = activityRegistry.Find(a.ActivityType)?.CustomProperties.TryGetValue(WorkflowsConstants.FieldDependenciesDescriptorProperty, out var declared) == true
                ? declared as IReadOnlyList<WorkflowFieldDependencyDescriptor>
                : null,
        }).ToList();

        return Ok(new WorkflowRegistryModel(catalog.Triggers, activities, flows, catalog.Connectors, catalog.HookSlots));
    }

    /// <summary>
    /// Re-runs the shipped-flow sync the tenant runs at activation: installs what is missing,
    /// re-publishes an unpublished system flow, upgrades unforked copies behind the shipped
    /// version. Idempotent; answers what it did per flow.
    /// </summary>
    [HttpPost(WorkflowsConstants.Routes.RegistrySyncApi)]
    public async Task<IActionResult> SyncAsync(CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ManageShippedWorkflows))
        {
            return Forbid();
        }

        var actions = await importer.SyncAsync(cancellationToken);
        return Ok(actions.Select(a => new { a.FlowKey, Action = a.Action.ToString() }));
    }

    /// <summary>
    /// Re-imports a shipped flow over the tenant's copy (plans/workflows.md, phase 5): a new
    /// version from the shipped JSON, fork mark cleared, the tenant's edits kept in history.
    /// System flows are never behind, so there is nothing to reset.
    /// </summary>
    [HttpPost(WorkflowsConstants.Routes.RegistryFlowResetApi)]
    public async Task<IActionResult> ResetAsync(string key, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ManageShippedWorkflows))
        {
            return Forbid();
        }

        var flow = catalog.FindFlow(key);
        if (flow is null)
        {
            return NotFound();
        }

        if (WorkflowOwnership.Normalize(flow.Ownership) == WorkflowOwnership.System)
        {
            return BadRequest($"'{flow.Key}' is a system workflow: it is kept at the shipped version by code and cannot be reset.");
        }

        if (!await importer.ResetAsync(flow.Key, cancellationToken))
        {
            return Problem($"Shipped flow '{flow.Key}' did not import; see the log.", statusCode: StatusCodes.Status500InternalServerError);
        }

        var found = await lookup.FindAsync(flow.Key, cancellationToken);
        return Ok(new { flow.Key, found?.DefinitionId, Published = found?.Published ?? false, Version = flow.Version });
    }
}
