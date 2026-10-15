using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Fields;
using Grove.Contents.Flows.Models;
using Grove.Models;
using Grove.Parties.Constants;

namespace Grove.Parties.Services;

/// <summary>
/// One kinded bag on a party: which BagPart, which element type lives in it, which
/// option list types its entries, and which boolean field marks the one entry per kind
/// (exclusive: setting it on one entry clears it on the others of that kind).
/// </summary>
public sealed record PartyBag(string BagName, string ElementType, string KindList, string ExclusiveField);

/// <summary>The kinded bags Parties owns.</summary>
public static class PartyBags
{
    public static readonly PartyBag ContactPoints = new(
        PartiesConstants.Bags.ContactPoints, PartiesConstants.ContentTypes.ContactPoint, PartiesOptionSets.ContactPointKind, "Preferred");

    public static readonly PartyBag Addresses = new(
        PartiesConstants.Bags.Addresses, PartiesConstants.ContentTypes.Address, PartiesOptionSets.AddressKind, "Preferred");

    public static readonly PartyBag OrganizationNames = new(
        PartiesConstants.Bags.OrganizationNames, PartiesConstants.ContentTypes.OrganizationName, PartiesOptionSets.OrganizationNameKind, "Primary");
}

/// <summary>
/// The one write path for bag elements on a party (ruling 2026-10-06). The platform
/// stores bags and edits them only through its MVC editor, with no API to add, change
/// or remove an element; this is that API plus Grove's rules: the kind travels as an
/// option KEY, one exclusive entry per kind, and the write publishes (bag contents are
/// live data, never a draft that does not reach the published version). Callers hand in
/// a party they have loaded and authorized; this never authorizes. Element-specific
/// fields are applied by the caller's <c>apply</c> callback.
/// </summary>
public sealed class PartyBagElementService(IContentManager contentManager, PartyOptionKeys optionKeys)
{
    public IReadOnlyList<ContentItem> Elements(ContentItem party, PartyBag bag) =>
        party.Get<BagPart>(bag.BagName)?.ContentItems ?? [];

    /// <summary>The element's kind as an option key; null when unset or no longer in the list.</summary>
    public Task<string?> KindKeyAsync(ContentItem element, PartyBag bag, CancellationToken cancellationToken = default) =>
        optionKeys.KeyForIdAsync(bag.KindList, PickerId(element, bag.ElementType, "Kind"), cancellationToken);

    /// <summary>The option id for a kind key of the bag's list; an unknown key is a caller error.</summary>
    public async Task<string> RequireKindIdAsync(PartyBag bag, string kindKey, CancellationToken cancellationToken = default) =>
        (await optionKeys.RequireOptionAsync(bag.KindList, kindKey, cancellationToken)).ContentItemId;

    public async Task<ContentItem> AddAsync(ContentItem party, PartyBag bag, string kindId, bool exclusive, Action<ContentItem> apply)
    {
        var element = await contentManager.NewAsync(bag.ElementType);
        SetPicker(element, bag.ElementType, "Kind", bag.KindList, kindId);
        apply(element);
        SetFlag(element, bag.ElementType, bag.ExclusiveField, exclusive);

        party.Alter<BagPart>(bag.BagName, part =>
        {
            if (exclusive)
            {
                ClearOthers(part, bag, element.ContentItemId, kindId);
            }

            part.ContentItems.Add(element);
        });

        await SaveAsync(party);
        return element;
    }

    /// <summary>Null when no element with that id is in the bag.</summary>
    public async Task<ContentItem?> UpdateAsync(ContentItem party, PartyBag bag, string elementId, string kindId, bool exclusive, Action<ContentItem> apply)
    {
        ContentItem? updated = null;
        party.Alter<BagPart>(bag.BagName, part =>
        {
            updated = part.ContentItems.FirstOrDefault(item => item.ContentItemId == elementId);
            if (updated is null)
            {
                return;
            }

            SetPicker(updated, bag.ElementType, "Kind", bag.KindList, kindId);
            apply(updated);
            SetFlag(updated, bag.ElementType, bag.ExclusiveField, exclusive);
            if (exclusive)
            {
                ClearOthers(part, bag, updated.ContentItemId, kindId);
            }
        });

        if (updated is not null)
        {
            await SaveAsync(party);
        }

        return updated;
    }

    public async Task<bool> RemoveAsync(ContentItem party, PartyBag bag, string elementId)
    {
        var removed = false;
        party.Alter<BagPart>(bag.BagName, part => removed = part.ContentItems.RemoveAll(item => item.ContentItemId == elementId) > 0);

        if (removed)
        {
            await SaveAsync(party);
        }

        return removed;
    }

    /// <summary>
    /// Saves a party whose bag was changed, and publishes it: the write and the publish
    /// happen together (ruling 2026-10-06), so indexes and consumers that read published
    /// versions see the change. Positions share this path.
    /// </summary>
    public async Task SaveAsync(ContentItem party)
    {
        await contentManager.UpdateAsync(party);
        if (!party.Published)
        {
            await contentManager.PublishAsync(party);
        }
    }

    // One exclusive entry per kind: marking one clears the rest of that kind.
    private static void ClearOthers(BagPart part, PartyBag bag, string exceptId, string kindId)
    {
        foreach (var sibling in part.ContentItems)
        {
            if (sibling.ContentItemId != exceptId && PickerId(sibling, bag.ElementType, "Kind") == kindId)
            {
                SetFlag(sibling, bag.ElementType, bag.ExclusiveField, false);
            }
        }
    }

    // --- element field access, shared by every bag's element-specific code ---

    public static string? Text(ContentItem element, string part, string field) =>
        element.Get<ContentPart>(part)?.Get<TextField>(field)?.Text;

    public static bool Flag(ContentItem element, string part, string field) =>
        element.Get<ContentPart>(part)?.Get<BooleanField>(field)?.Value ?? false;

    public static string? PickerId(ContentItem element, string part, string field) =>
        element.Get<ContentPart>(part)?.Get<OptionPickerField>(field)?.SelectedIds.FirstOrDefault();

    public static void SetText(ContentItem element, string part, string field, string? value) =>
        element.Alter<ContentPart>(part, p => p.Alter<TextField>(field, f => f.Text = string.IsNullOrWhiteSpace(value) ? null : value.Trim()));

    public static void SetFlag(ContentItem element, string part, string field, bool value) =>
        element.Alter<ContentPart>(part, p => p.Alter<BooleanField>(field, f => f.Value = value));

    public static void SetPicker(ContentItem element, string part, string field, string listKey, string? optionId) =>
        element.Alter<ContentPart>(part, p => p.Alter<OptionPickerField>(field, f =>
        {
            f.SourceKey = OptionSourceKeys.ForContentPartList(listKey);
            f.SelectedIds = optionId is null ? [] : [optionId];
        }));
}
