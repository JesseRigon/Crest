using System.Security.Claims;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Registry;
using Grove.Workflows.Security;
using Grove.Workflows.Units;
using Grove.Data.Documents;
using Grove.Documents;
using Grove.Environment.Cache;

namespace Grove.Workflows.Hooks;

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
    WorkflowDefinitionAccessService access,
    IEnumerable<IWorkflowHookSlotSource> slotSources,
    WorkflowHookAttachmentCache cache,
    Grove.Environment.Cache.ISignal signal,
    ShippedFlowsInstaller installer,
    IServiceProvider services)
{
    /// <summary>A slot by key: a module's (the registry's) or a slot source's (a content type's lifecycle point).</summary>
    public async Task<WorkflowHookSlotDescriptor?> FindSlotAsync(string slotKey, CancellationToken cancellationToken = default)
    {
        if (catalog.FindHookSlot(slotKey) is { } slot)
        {
            return slot;
        }

        foreach (var source in slotSources)
        {
            if (await source.FindHookSlotAsync(slotKey, cancellationToken) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// A slot's attachments, system first. A system attachment whose flow is not installed is
    /// listed with no definition (<see cref="IsMissing"/>): the runner refuses on it rather than
    /// skipping it. Resolved once per slot and cached until a slot source's stamp changes or
    /// <see cref="WorkflowHookAttachmentCache.SignalKey"/> is raised.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowHookAttachmentModel>> ListAsync(string slotKey, CancellationToken cancellationToken = default)
    {
        var stamp = await StampAsync(cancellationToken);
        if (cache.TryGet(slotKey, stamp, out var cached))
        {
            return cached;
        }

        var token = cache.Token();
        var attachments = await ResolveAsync(slotKey, cancellationToken);

        // A system flow is missing while the shell has not installed its shipped flows yet (an item
        // saved by a feature's own seeding, before the activation's task ran): install them, then
        // read the slot again. What is still missing then is refused by the runner.
        if (!installer.Installed && attachments.Any(IsMissing))
        {
            await installer.EnsureAsync(services, cancellationToken);
            return await ResolveAsync(slotKey, cancellationToken);
        }

        cache.Set(slotKey, stamp, token, attachments);
        return attachments;
    }

    /// <summary>A system attachment whose shipped flow is not installed in the tenant.</summary>
    public static bool IsMissing(WorkflowHookAttachmentModel attachment) => string.IsNullOrEmpty(attachment.DefinitionId);

    /// <summary>Raised when what a slot's attachments resolve to changes, once the unit ends.</summary>
    public void Invalidate() => signal.DeferredSignalToken(WorkflowHookAttachmentCache.SignalKey);

    private async Task<string> StampAsync(CancellationToken cancellationToken)
    {
        var stamps = new List<string>();
        foreach (var source in slotSources)
        {
            stamps.Add(await source.StampAsync(cancellationToken));
        }

        return string.Join('|', stamps);
    }

    private async Task<IReadOnlyList<WorkflowHookAttachmentModel>> ResolveAsync(string slotKey, CancellationToken cancellationToken)
    {
        var slot = await FindSlotAsync(slotKey, cancellationToken);
        if (slot is null)
        {
            return [];
        }

        var systemAttachments = catalog.HookAttachments.Where(a => string.Equals(a.Slot, slot.Key, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var source in slotSources)
        {
            systemAttachments.AddRange(await source.ListHookAttachmentsAsync(slot, cancellationToken));
        }

        var result = new List<WorkflowHookAttachmentModel>();
        foreach (var system in systemAttachments.DistinctBy(a => a.FlowKey, StringComparer.OrdinalIgnoreCase).OrderBy(a => a.Position))
        {
            var installed = await flows.FindAsync(system.FlowKey, cancellationToken);
            result.Add(new(null, slot.Key, installed?.DefinitionId ?? string.Empty, catalog.FindFlow(system.FlowKey)?.DisplayName, system.FlowKey, system.Position, system.Required, WorkflowOwnership.System));
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
        var slots = catalog.HookSlots.ToList();
        foreach (var source in slotSources)
        {
            slots.AddRange(await source.ListHookSlotsAsync(cancellationToken));
        }

        var result = new List<WorkflowHookSlotModel>();
        foreach (var slot in slots.DistinctBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
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
    public async Task<WorkflowHookAttachmentModel> AttachAsync(WorkflowHookAttachRequest request, CancellationToken cancellationToken = default)
    {
        var slot = await FindSlotAsync(request.Slot, cancellationToken) ?? throw new WorkflowHookAttachException($"'{request.Slot}' is not a registered hook slot.");
        if (!slot.AllowTenantAttachments)
        {
            throw new WorkflowHookAttachException($"Hook slot '{slot.Key}' is system-only: the owning module decides what runs there.");
        }

        var definition = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = request.DefinitionId, VersionOptions = VersionOptions.Published }, cancellationToken)
            ?? throw new WorkflowHookAttachException("The flow must be published before it is attached to a hook.");

        if (!await access.AuthorizeAsync(WorkflowOwnershipGuard.PermissionFor(WorkflowChange.Save, WorkflowOwnershipInfo.From(definition.CustomProperties)), request.DefinitionId))
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
        Invalidate();
        return new(attachment.Id, slot.Key, attachment.DefinitionId, definition.Name, null, attachment.Position, attachment.Required, WorkflowOwnership.Tenant);
    }

    public async Task<bool> DetachAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetOrCreateMutableAsync();
        var attachment = document.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
        {
            return false;
        }

        var definition = await definitions.FindAsync(new WorkflowDefinitionFilter { DefinitionId = attachment.DefinitionId, VersionOptions = VersionOptions.Latest }, cancellationToken);
        if (definition is not null && !await access.AuthorizeAsync(WorkflowOwnershipGuard.PermissionFor(WorkflowChange.Save, WorkflowOwnershipInfo.From(definition.CustomProperties)), attachment.DefinitionId))
        {
            throw new WorkflowHookAttachException("You may not detach a flow you cannot edit.");
        }

        document.Attachments.Remove(attachment);
        await documents.UpdateAsync(document);
        Invalidate();
        return true;
    }

    /// <summary>
    /// The flows a tenant may offer for attachment: published, and editable by the caller (the
    /// attach checks the rest - the slot's policy and the flow's atomicity).
    /// </summary>
    public async Task<IReadOnlyList<WorkflowHookCandidateModel>> ListCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<WorkflowHookCandidateModel>();
        foreach (var definition in await definitions.FindManyAsync(new WorkflowDefinitionFilter { VersionOptions = VersionOptions.Published }, cancellationToken))
        {
            if (await access.AuthorizeAsync(WorkflowOwnershipGuard.PermissionFor(WorkflowChange.Save, WorkflowOwnershipInfo.From(definition.CustomProperties)), definition.DefinitionId))
            {
                result.Add(new(definition.DefinitionId, definition.Name ?? definition.DefinitionId));
            }
        }

        return result.OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The tenant attachments that use a definition, for the publish-time check.</summary>
    public async Task<IReadOnlyList<WorkflowHookAttachment>> AttachmentsOfAsync(string definitionId)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Attachments.Where(a => a.DefinitionId == definitionId).ToArray();
    }
}
