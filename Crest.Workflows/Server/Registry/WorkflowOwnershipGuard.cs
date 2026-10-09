using System.Text.Json;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Parts;
using Crest.Workflows.Security;
using Crest.Workflows.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.Security.Permissions;

namespace Crest.Workflows.Registry;

/// <summary>What a definition's custom properties say about who owns it (docs/workflows.md, phase 5).</summary>
public sealed record WorkflowOwnershipInfo(string Ownership, string? FlowKey, int? FlowVersion, bool Forked)
{
    public static readonly WorkflowOwnershipInfo TenantOwned = new(WorkflowOwnership.Tenant, null, null, false);

    public bool IsSystem => Ownership == WorkflowOwnership.System;
    public bool IsShipped => Ownership == WorkflowOwnership.Shipped;
    public bool IsTenant => Ownership == WorkflowOwnership.Tenant;

    public static WorkflowOwnershipInfo From(IDictionary<string, object>? properties)
    {
        if (properties is null)
        {
            return TenantOwned;
        }

        return new(
            WorkflowOwnership.Normalize(Text(properties, WorkflowsConstants.OwnershipProperty)),
            Text(properties, WorkflowsConstants.FlowKeyProperty),
            int.TryParse(Text(properties, WorkflowsConstants.FlowVersionProperty), out var version) ? version : null,
            bool.TryParse(Text(properties, WorkflowsConstants.ForkedProperty), out var forked) && forked);
    }

    /// <summary>Writes this ownership onto a definition's custom properties, replacing whatever the client sent.</summary>
    public void Stamp(IDictionary<string, object> properties)
    {
        properties.Remove(WorkflowsConstants.OwnershipProperty);
        properties.Remove(WorkflowsConstants.FlowKeyProperty);
        properties.Remove(WorkflowsConstants.FlowVersionProperty);
        properties.Remove(WorkflowsConstants.ForkedProperty);
        if (IsTenant)
        {
            return;
        }

        properties[WorkflowsConstants.OwnershipProperty] = Ownership;
        if (FlowKey is not null) properties[WorkflowsConstants.FlowKeyProperty] = FlowKey;
        if (FlowVersion is not null) properties[WorkflowsConstants.FlowVersionProperty] = FlowVersion.Value;
        if (Forked) properties[WorkflowsConstants.ForkedProperty] = true;
    }

    // Values arrive as JsonElement from the stored JSON and as CLR values from the importer.
    public static string? Text(IDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement element => element.GetRawText(),
            _ => value.ToString(),
        };
    }
}

/// <summary>The change a user asks of a definition; the tier decides whether it may happen.</summary>
public enum WorkflowChange
{
    Save,
    Publish,
    Retract,
    Delete,
}

/// <summary>A user asked for a change the definition's ownership tier or access lists forbid. The API gate answers 403 with the message.</summary>
public sealed class WorkflowChangeDeniedException(string message) : InvalidOperationException(message);

/// <summary>
/// Enforces the ownership tiers at the store, not the UI. The stored definition's tier
/// decides (never the incoming model's: a client could strip the properties), the stored
/// ownership is stamped back onto what gets saved, and a user's save of a shipped flow marks
/// the fork. Then the acting user (the request's principal; a change with no request is the
/// system's own) is authorized against the definition in one Orchard call - the permission
/// the change needs, with the definition's access lists as the resource - so the role grant,
/// the super user, the member ceiling and the per-flow lists all decide together. The
/// importer's own writes run inside <see cref="WorkflowSystemScope"/> and are exempt: that
/// is how system flows get written at all.
/// </summary>
public sealed class WorkflowOwnershipGuard(
    IContentManager contentManager,
    WorkflowDefinitionPartMapper partMapper,
    IHttpContextAccessor httpContextAccessor,
    WorkflowDefinitionAccessService access)
{
    public async Task<WorkflowOwnershipInfo> GetStoredAsync(string? definitionId) => WorkflowOwnershipInfo.From(await GetStoredPropertiesAsync(definitionId));

    private async Task<IDictionary<string, object>?> GetStoredPropertiesAsync(string? definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
        {
            return null;
        }

        var contentItem = await contentManager.GetAsync(definitionId, VersionOptions.Latest);
        var part = contentItem?.Get<WorkflowDefinitionPart>(typeof(WorkflowDefinitionPart).Name);
        return part is null || part.SerializedData is null ? null : partMapper.MapModel(part).CustomProperties;
    }

    /// <summary>
    /// Refuses the change when the tier or the user's permissions forbid it (throws), else
    /// returns the ownership the saved definition must carry: the stored one, forked when a
    /// user saves a shipped flow.
    /// </summary>
    public async Task<WorkflowOwnershipInfo> AuthorizeChangeAsync(string? definitionId, WorkflowChange change)
    {
        var properties = await GetStoredPropertiesAsync(definitionId);
        var stored = WorkflowOwnershipInfo.From(properties);
        if (WorkflowSystemScope.IsActive)
        {
            return stored;
        }

        if (stored.IsSystem)
        {
            throw new WorkflowChangeDeniedException($"Workflow '{stored.FlowKey}' is a system workflow: it is maintained by code and cannot be {Verb(change)} by a user.");
        }

        if (stored.IsShipped && change == WorkflowChange.Delete)
        {
            throw new WorkflowChangeDeniedException($"Workflow '{stored.FlowKey}' is a shipped workflow: it can be edited, published or reset to the shipped version, not deleted.");
        }

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var permission = PermissionFor(change, stored);
            var resource = WorkflowDefinitionAccessResource.From(definitionId ?? string.Empty, properties);
            if (!await access.AuthorizeAsync(user, permission, resource))
            {
                throw new WorkflowChangeDeniedException($"'{user.Identity.Name}' may not have this workflow {Verb(change)}: it takes {permission.Name}{(resource.Edit.Count > 0 ? " and the workflow names who may edit it" : string.Empty)}.");
            }
        }

        return stored.IsShipped && change == WorkflowChange.Save ? stored with { Forked = true } : stored;
    }

    /// <summary>Publishing and retracting take Publish; saving, reverting and deleting take Edit, or Manage shipped for a shipped flow.</summary>
    public static Permission PermissionFor(WorkflowChange change, WorkflowOwnershipInfo stored) => change switch
    {
        WorkflowChange.Publish or WorkflowChange.Retract => Permissions.PublishWorkflows,
        _ => stored.IsShipped ? Permissions.ManageShippedWorkflows : Permissions.EditWorkflows,
    };

    /// <summary>Applies the authorized ownership onto the definition about to be saved.</summary>
    public async Task<WorkflowOwnershipInfo> PrepareAsync(WorkflowDefinition definition, WorkflowChange change)
    {
        definition.CustomProperties ??= new Dictionary<string, object>();
        WorkflowOwnershipInfo ownership;
        if (WorkflowSystemScope.IsActive)
        {
            // The importer decides the properties (a new version, a cleared fork mark).
            ownership = WorkflowOwnershipInfo.From(definition.CustomProperties);
        }
        else
        {
            ownership = await AuthorizeChangeAsync(definition.DefinitionId, change);
            ownership.Stamp(definition.CustomProperties);
        }

        if (ownership.IsSystem)
        {
            definition.IsReadonly = true;
        }

        return ownership;
    }

    private static string Verb(WorkflowChange change) => change switch
    {
        WorkflowChange.Save => "changed",
        WorkflowChange.Publish => "published",
        WorkflowChange.Retract => "retracted",
        WorkflowChange.Delete => "deleted",
        _ => "changed",
    };
}

/// <summary>
/// Marks writes made by the system itself (the shipped-flow importer, upgrades, resets) so
/// the ownership guard lets them through. Flows with the async call chain, so a scope
/// opened around an import covers everything the engine does inside it.
/// </summary>
public static class WorkflowSystemScope
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool IsActive => Depth.Value > 0;

    public static IDisposable Begin()
    {
        Depth.Value++;
        return new Exit();
    }

    private sealed class Exit : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Depth.Value--;
        }
    }
}
