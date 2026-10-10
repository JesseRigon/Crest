using System.Text.Json;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Management.Models;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Registry;

/// <summary>
/// Brings the tenant's copies of the shipped flows in line with what the modules ship
/// (docs/workflows.md, phase 5). Runs from the deferred task that populates the activity
/// registry, right after it (the definition JSON names activity types, which must be
/// registered before it can be read). A flow is found by its key in the definition's custom
/// properties; the shipped JSON is the engine's definition-model format.
/// <list type="bullet">
/// <item>Missing: imported. A system flow is published; a shipped one as the descriptor says.</item>
/// <item>Older than shipped and unforked: upgraded, as a new version of the same definition
/// (running instances finish on theirs). Published if the installed one was, or for a system
/// flow.</item>
/// <item>Forked (a tenant user saved it): left alone. The registry reports it outdated;
/// <see cref="ResetAsync"/> re-imports it on request.</item>
/// </list>
/// Every write runs in <see cref="WorkflowSystemScope"/>, which is what lets it write system
/// flows and keeps upgrades from counting as forks.
/// </summary>
public sealed class WorkflowFlowImporter(
    WorkflowRegistryCatalog catalog,
    IWorkflowDefinitionImporter importer,
    IApiSerializer serializer,
    WorkflowFlowLookup lookup,
    ILogger<WorkflowFlowImporter> logger)
{
    public async Task<IReadOnlyList<(string FlowKey, WorkflowFlowSyncAction Action)>> SyncAsync(CancellationToken cancellationToken = default)
    {
        var actions = new List<(string, WorkflowFlowSyncAction)>();
        foreach (var flow in catalog.Flows)
        {
            var installed = await lookup.FindAsync(flow.Key, cancellationToken);
            var action = Decide(flow, installed);
            actions.Add((flow.Key, action));
            switch (action)
            {
                case WorkflowFlowSyncAction.Install:
                    await ImportAsync(flow, null, publish: IsSystem(flow) || flow.Publish, cancellationToken);
                    break;
                case WorkflowFlowSyncAction.Republish:
                    // A system flow is always published: one that is not (a stray draft became the
                    // latest version) is re-imported and published.
                    logger.LogWarning("System flow {Flow} is not published: re-importing.", flow.Key);
                    await ImportAsync(flow, installed, publish: true, cancellationToken);
                    break;
                case WorkflowFlowSyncAction.Upgrade:
                    logger.LogInformation("Shipped flow {Flow} is at version {Installed}, shipped is {Shipped}: upgrading.", flow.Key, installed!.Ownership.FlowVersion ?? 1, flow.Version);
                    await ImportAsync(flow, installed, publish: IsSystem(flow) || installed.Published, cancellationToken);
                    break;
            }
        }

        return actions;
    }

    /// <summary>
    /// What a sync does for one shipped flow, from what is installed: install when absent;
    /// re-publish an unpublished system flow; upgrade an unforked copy behind the shipped
    /// version; otherwise keep (a forked copy is the tenant's until reset).
    /// </summary>
    public static WorkflowFlowSyncAction Decide(WorkflowFlowDescriptor flow, WorkflowFlowInstallation? installed)
    {
        if (installed is null)
        {
            return WorkflowFlowSyncAction.Install;
        }

        if (IsSystem(flow) && !installed.Published)
        {
            return WorkflowFlowSyncAction.Republish;
        }

        if (installed.Ownership.Forked || (installed.Ownership.FlowVersion ?? 1) >= flow.Version)
        {
            return WorkflowFlowSyncAction.Keep;
        }

        return WorkflowFlowSyncAction.Upgrade;
    }

    /// <summary>
    /// Re-imports the shipped JSON over the tenant's copy as a new version and clears the fork
    /// mark; the tenant's edits stay in the version history. Published if the copy was.
    /// </summary>
    public async Task<bool> ResetAsync(string flowKey, CancellationToken cancellationToken = default)
    {
        var flow = catalog.FindFlow(flowKey);
        if (flow is null)
        {
            return false;
        }

        var installed = await lookup.FindAsync(flow.Key, cancellationToken);
        return await ImportAsync(flow, installed, publish: IsSystem(flow) || (installed?.Published ?? flow.Publish), cancellationToken);
    }

    private static bool IsSystem(WorkflowFlowDescriptor flow) => WorkflowOwnership.Normalize(flow.Ownership) == WorkflowOwnership.System;

    private async Task<bool> ImportAsync(WorkflowFlowDescriptor flow, WorkflowFlowInstallation? installed, bool publish, CancellationToken cancellationToken)
    {
        WorkflowDefinitionModel model;
        try
        {
            model = JsonSerializer.Deserialize<WorkflowDefinitionModel>(flow.DefinitionJson, serializer.GetOptions())
                ?? throw new InvalidOperationException("the JSON is empty");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shipped flow {Flow} is not a readable workflow definition model.", flow.Key);
            return false;
        }

        var ownership = new WorkflowOwnershipInfo(WorkflowOwnership.Normalize(flow.Ownership), flow.Key, flow.Version, Forked: false);
        model.Name ??= flow.DisplayName;
        model.DefinitionId = installed?.DefinitionId!;
        model.Id = null!;
        model.CustomProperties ??= new Dictionary<string, object>();
        ownership.Stamp(model.CustomProperties);
        model.IsReadonly = ownership.IsSystem;
        model.Variables ??= [];
        model.Inputs ??= [];
        model.Outputs ??= [];
        model.Outcomes ??= [];

        ImportWorkflowResult result;
        using (WorkflowSystemScope.Begin())
        {
            result = await importer.ImportAsync(new SaveWorkflowDefinitionRequest { Model = model, Publish = publish }, cancellationToken);
        }

        if (!result.Succeeded)
        {
            logger.LogError("Shipped flow {Flow} did not import: {Errors}", flow.Key, string.Join("; ", result.ValidationErrors.Select(e => e.Message)));
            return false;
        }

        logger.LogInformation("Shipped flow {Flow} v{Version} imported as definition {DefinitionId} ({Ownership}).", flow.Key, flow.Version, result.WorkflowDefinition.DefinitionId, ownership.Ownership);
        return true;
    }
}

/// <summary>The tenant's copy of a shipped flow: latest version, with its ownership properties.</summary>
public sealed record WorkflowFlowInstallation(string DefinitionId, bool Published, WorkflowOwnershipInfo Ownership);

public enum WorkflowFlowSyncAction { Install, Republish, Upgrade, Keep }

/// <summary>Finds the tenant's definition for a shipped flow key (latest version).</summary>
public sealed class WorkflowFlowLookup(IWorkflowDefinitionStore store)
{
    public async Task<WorkflowFlowInstallation?> FindAsync(string flowKey, CancellationToken cancellationToken = default)
    {
        var definitions = await store.FindManyAsync(new WorkflowDefinitionFilter { VersionOptions = VersionOptions.Latest }, cancellationToken);
        var match = definitions.FirstOrDefault(d => string.Equals(WorkflowOwnershipInfo.Text(d.CustomProperties, WorkflowsConstants.FlowKeyProperty), flowKey, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : new(match.DefinitionId, match.IsPublished, WorkflowOwnershipInfo.From(match.CustomProperties));
    }

    public async Task<string?> FindDefinitionIdAsync(string flowKey, CancellationToken cancellationToken = default) => (await FindAsync(flowKey, cancellationToken))?.DefinitionId;
}
