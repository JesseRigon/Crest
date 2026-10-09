using System.Security.Claims;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Registry;
using Crest.Workflows.Security;
using Crest.Workflows.Units;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace Crest.Workflows.Hooks;

/// <summary>A tenant's attachment of a published definition to a hook slot.</summary>
public sealed class WorkflowHookAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Slot { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool Required { get; set; } = true;
}

public sealed class WorkflowHookAttachmentsDocument : Document
{
    public List<WorkflowHookAttachment> Attachments { get; set; } = [];
}

/// <summary>A refused attach: the slot's policy or the flow's shape. The API answers 400 with the message.</summary>
public sealed class WorkflowHookAttachException(string message) : Exception(message);

/// <summary>
/// Hook slots and what is attached to them (docs/workflows.md › Posting on workflows). System
/// attachments come from the registry (a module's shipped flow, by key, resolved to the
/// tenant's definition); tenant attachments live in one document. Both are listed in slot
/// order - system first, then tenant by position - and both must be atomic, which the
/// analyzer decides, never the author.
/// </summary>
public sealed class WorkflowHookService(
    WorkflowRegistryCatalog catalog,
    WorkflowFlowLookup flows,
    IDocumentManager<WorkflowHookAttachmentsDocument> documents,
    IWorkflowDefinitionStore definitions,
    WorkflowAtomicityAnalyzer analyzer,
    WorkflowDefinitionAccessService access)
{
    public async Task<IReadOnlyList<WorkflowHookAttachmentModel>> ListAsync(string slotKey, CancellationToken cancellationToken = default)
    {
        var slot = catalog.FindHookSlot(slotKey);
        if (slot is null)
        {
            return [];
        }

        var result = new List<WorkflowHookAttachmentModel>();
        foreach (var system in catalog.HookAttachments.Where(a => string.Equals(a.Slot, slot.Key, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a.Position))
        {
            var installed = await flows.FindAsync(system.FlowKey, cancellationToken);
            if (installed is null)
            {
                continue;
            }

            result.Add(new(null, slot.Key, installed.DefinitionId, catalog.FindFlow(system.FlowKey)?.DisplayName, system.FlowKey, system.Position, system.Required, WorkflowOwnership.System));
        }

        var document = await documents.GetOrCreateImmutableAsync();
        foreach (var tenant in document.Attachments.Where(a => string.Equals(a.Slot, slot.Key, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a.Position).ThenBy(a => a.Id, StringComparer.Ordinal))
        {
            var definition = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = tenant.DefinitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
            result.Add(new(tenant.Id, slot.Key, tenant.DefinitionId, definition?.Name, null, tenant.Position, tenant.Required, WorkflowOwnership.Tenant));
        }

        return result;
    }

    public async Task<IReadOnlyList<WorkflowHookSlotModel>> ListSlotsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<WorkflowHookSlotModel>();
        foreach (var slot in catalog.HookSlots)
        {
            result.Add(new(slot, await ListAsync(slot.Key, cancellationToken)));
        }

        return result;
    }

    /// <summary>
    /// Attaches a tenant flow: the slot must exist and allow tenant attachments, the flow must
    /// be published and atomic, and the user must be allowed to edit that flow (the per-flow
    /// access lists apply; the controller checked the permission). Required is forced on when
    /// the slot says RequiredOnly.
    /// </summary>
    public async Task<WorkflowHookAttachmentModel> AttachAsync(ClaimsPrincipal user, WorkflowHookAttachRequest request, CancellationToken cancellationToken = default)
    {
        var slot = catalog.FindHookSlot(request.Slot) ?? throw new WorkflowHookAttachException($"'{request.Slot}' is not a registered hook slot.");
        if (!slot.AllowTenantAttachments)
        {
            throw new WorkflowHookAttachException($"Hook slot '{slot.Key}' is system-only: the owning module decides what runs there.");
        }

        var definition = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = request.DefinitionId, VersionOptions = VersionOptions.Published }, cancellationToken)
            ?? throw new WorkflowHookAttachException("The flow must be published before it is attached to a hook.");

        if (!await access.AuthorizeAsync(user, WorkflowOwnershipGuard.PermissionFor(WorkflowChange.Save, WorkflowOwnershipInfo.From(definition.CustomProperties)), request.DefinitionId))
        {
            throw new WorkflowHookAttachException("You may not attach a flow you cannot edit.");
        }

        var report = await analyzer.AnalyzeAsync(request.DefinitionId, cancellationToken)
            ?? throw new WorkflowHookAttachException("The flow could not be analyzed.");
        if (!report.IsAtomic)
        {
            throw new WorkflowHookAttachException($"'{definition.Name}' is long-running and cannot run inside a hook, which is part of the host's transaction: {report.Describe()}. Attach it to an event (a trigger) instead, or move those steps out of it.");
        }

        var required = slot.RequiredOnly || request.Required;
        var document = await documents.GetOrCreateMutableAsync();
        var attachment = document.Attachments.FirstOrDefault(a => string.Equals(a.Slot, slot.Key, StringComparison.OrdinalIgnoreCase) && a.DefinitionId == request.DefinitionId);
        if (attachment is null)
        {
            attachment = new WorkflowHookAttachment { Slot = slot.Key, DefinitionId = request.DefinitionId };
            document.Attachments.Add(attachment);
        }

        attachment.Position = request.Position ?? attachment.Position;
        attachment.Required = required;
        await documents.UpdateAsync(document);
        return new(attachment.Id, slot.Key, attachment.DefinitionId, definition.Name, null, attachment.Position, attachment.Required, WorkflowOwnership.Tenant);
    }

    public async Task<bool> DetachAsync(ClaimsPrincipal user, string attachmentId, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        var attachment = document.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
        {
            return false;
        }

        var definition = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = attachment.DefinitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
        if (definition is not null && !await access.AuthorizeAsync(user, WorkflowOwnershipGuard.PermissionFor(WorkflowChange.Save, WorkflowOwnershipInfo.From(definition.CustomProperties)), attachment.DefinitionId))
        {
            throw new WorkflowHookAttachException("You may not detach a flow you cannot edit.");
        }

        document.Attachments.Remove(attachment);
        await documents.UpdateAsync(document);
        return true;
    }

    /// <summary>The tenant attachments that use a definition, for the publish-time check.</summary>
    public async Task<IReadOnlyList<WorkflowHookAttachment>> AttachmentsOfAsync(string definitionId)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Attachments.Where(a => a.DefinitionId == definitionId).ToArray();
    }
}
