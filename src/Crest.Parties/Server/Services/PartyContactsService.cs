using Crest.Fields;
using Crest.Global.Lists;
using Crest.Models;
using Crest.Regions.Fields;
using Crest.Regions.Models;
using Crest.Regions.Services;
using Crest.Parties.Constants;
using Crest.Parties.ViewModels;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.Flows.Models;

namespace Crest.Parties.Services;

public sealed class PartyContactsService(
    IContentManager contentManager,
    PartyOptionKeys optionKeys,
    IGeoService geo,
    IGeoLocator locator) : IPartyContactsService
{
    private const string GlobalPhoneCountryCodes = GlobalLists.PhoneCountryCodes;

    public async Task<PartyContactsModel> ReadAsync(ContentItem party, CancellationToken cancellationToken = default)
    {
        var contactPoints = new List<ContactPointModel>();
        foreach (var element in Elements(party, PartiesConstants.Bags.ContactPoints))
        {
            contactPoints.Add(await ToContactPointAsync(element, cancellationToken));
        }

        var addresses = new List<AddressModel>();
        foreach (var element in Elements(party, PartiesConstants.Bags.Addresses))
        {
            addresses.Add(await ToAddressAsync(element, cancellationToken));
        }

        return new PartyContactsModel(contactPoints, addresses);
    }

    public async Task<ContactPointModel> AddContactPointAsync(ContentItem party, ContactPointWriteModel write, CancellationToken cancellationToken = default)
    {
        var ids = await ResolveContactPointIdsAsync(write, cancellationToken);
        var element = await contentManager.NewAsync(PartiesConstants.ContentTypes.ContactPoint);
        ApplyContactPoint(element, write, ids.KindId, ids.PhoneCountryId);

        AppendElement(party, PartiesConstants.Bags.ContactPoints, element, write.Preferred, ids.KindId, PartiesConstants.ContentTypes.ContactPoint);
        await contentManager.UpdateAsync(party);

        return await ToContactPointAsync(element, cancellationToken);
    }

    public async Task<ContactPointModel?> UpdateContactPointAsync(ContentItem party, string contactPointId, ContactPointWriteModel write, CancellationToken cancellationToken = default)
    {
        var ids = await ResolveContactPointIdsAsync(write, cancellationToken);
        ContentItem? updated = null;

        party.Alter<BagPart>(PartiesConstants.Bags.ContactPoints, bag =>
        {
            updated = bag.ContentItems.FirstOrDefault(item => item.ContentItemId == contactPointId);
            if (updated is null)
            {
                return;
            }

            ApplyContactPoint(updated, write, ids.KindId, ids.PhoneCountryId);
            if (write.Preferred)
            {
                ClearOtherPreferred(bag, updated.ContentItemId, ids.KindId, PartiesConstants.ContentTypes.ContactPoint);
            }
        });

        if (updated is null)
        {
            return null;
        }

        await contentManager.UpdateAsync(party);
        return await ToContactPointAsync(updated, cancellationToken);
    }

    public async Task<bool> RemoveContactPointAsync(ContentItem party, string contactPointId, CancellationToken cancellationToken = default)
        => await RemoveElementAsync(party, PartiesConstants.Bags.ContactPoints, contactPointId);

    public async Task<AddressModel> AddAddressAsync(ContentItem party, AddressWriteModel write, CancellationToken cancellationToken = default)
    {
        var ids = await ResolveAddressAsync(write, cancellationToken);
        var element = await contentManager.NewAsync(PartiesConstants.ContentTypes.Address);
        ApplyAddress(element, write, ids);

        AppendElement(party, PartiesConstants.Bags.Addresses, element, write.Preferred, ids.KindId, PartiesConstants.ContentTypes.Address);
        await contentManager.UpdateAsync(party);

        return await ToAddressAsync(element, cancellationToken);
    }

    public async Task<AddressModel?> UpdateAddressAsync(ContentItem party, string addressId, AddressWriteModel write, CancellationToken cancellationToken = default)
    {
        var ids = await ResolveAddressAsync(write, cancellationToken);
        ContentItem? updated = null;

        party.Alter<BagPart>(PartiesConstants.Bags.Addresses, bag =>
        {
            updated = bag.ContentItems.FirstOrDefault(item => item.ContentItemId == addressId);
            if (updated is null)
            {
                return;
            }

            ApplyAddress(updated, write, ids);
            if (write.Preferred)
            {
                ClearOtherPreferred(bag, updated.ContentItemId, ids.KindId, PartiesConstants.ContentTypes.Address);
            }
        });

        if (updated is null)
        {
            return null;
        }

        await contentManager.UpdateAsync(party);
        return await ToAddressAsync(updated, cancellationToken);
    }

    public async Task<bool> RemoveAddressAsync(ContentItem party, string addressId, CancellationToken cancellationToken = default)
        => await RemoveElementAsync(party, PartiesConstants.Bags.Addresses, addressId);

    private async Task<(string KindId, string? PhoneCountryId)> ResolveContactPointIdsAsync(ContactPointWriteModel write, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(write.Value))
        {
            throw new InvalidOperationException("A contact point needs a value.");
        }

        var kindId = (await optionKeys.RequireOptionAsync(PartiesOptionSets.ContactPointKind, write.Kind, cancellationToken)).ContentItemId;
        var phoneCountryId = await optionKeys.IdForKeyAsync(GlobalPhoneCountryCodes, write.PhoneCountry, cancellationToken);
        return (kindId, phoneCountryId);
    }

    // The address's country selects its addressing map; the map validates the text
    // (required fields, postal pattern) and the tree validates the stack (every node
    // exists, one per level, each beneath the one above). Both run server-side even
    // though the WASM client runs the same map rules, because an API write can skip the
    // form (docs/regions.md › Validation).
    private async Task<ResolvedAddress> ResolveAddressAsync(AddressWriteModel write, CancellationToken cancellationToken)
    {
        var kindId = (await optionKeys.RequireOptionAsync(PartiesOptionSets.AddressKind, write.Kind, cancellationToken)).ContentItemId;

        var country = write.Country?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(country))
        {
            return new ResolvedAddress(kindId, GeoStack.Empty, [], null, null);
        }

        var map = await geo.GetAddressingMapAsync(country, cancellationToken);
        var levels = new Dictionary<int, string?> { [1] = country };
        foreach (var (level, nodeId) in write.LevelNodeIds ?? new Dictionary<int, string?>())
        {
            if (level > 1 && !string.IsNullOrWhiteSpace(nodeId))
            {
                levels[level] = nodeId.Trim();
            }
        }

        // The postal-code record fills in levels the form did not: a code that maps to
        // exactly one node at a level names that node. A straddling code (two counties)
        // names nothing at that level and the form's choice stands.
        var postal = string.IsNullOrWhiteSpace(write.PostalCode) ? null : await geo.GetPostalCodeAsync(country, write.PostalCode, cancellationToken);
        if (postal is not null)
        {
            var byLevel = new Dictionary<int, List<string>>();
            foreach (var nodeId in postal.NodeIds)
            {
                if (await geo.GetNodeAsync(nodeId, cancellationToken) is { Kind: GeoNodeKinds.Level } node)
                {
                    (byLevel.TryGetValue(node.Level, out var list) ? list : byLevel[node.Level] = []).Add(nodeId);
                }
            }

            foreach (var (level, nodeIds) in byLevel)
            {
                if (level > 1 && nodeIds.Count == 1 && !levels.ContainsKey(level))
                {
                    levels[level] = nodeIds[0];
                }
            }
        }

        var input = new AddressInput(country, write.Line1, write.Line2, write.Locality, write.PostalCode, levels);
        var errors = AddressRules.Validate(map, input);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors.Select(error => error.Message)));
        }

        var stack = await geo.ResolveStackAsync(levels.Values.Where(id => id is not null).Select(id => id!), cancellationToken);

        // The point: supplied (picked on a map), else geocoded, else the postal centroid.
        // Boundaries follow from the point, never from the text.
        var point = write is { Latitude: not null, Longitude: not null }
            ? (write.Latitude.Value, write.Longitude.Value)
            : await locator.GeocodeAsync(input, cancellationToken)
              ?? (postal is { Latitude: not null, Longitude: not null } ? (postal.Latitude.Value, postal.Longitude.Value) : null);
        var boundaries = point is { } located
            ? await locator.BoundariesAsync(located.Item1, located.Item2, country, cancellationToken)
            : [];

        return new ResolvedAddress(kindId, stack, boundaries, point?.Item1, point?.Item2);
    }

    private sealed record ResolvedAddress(string KindId, GeoStack Stack, IReadOnlyList<string> BoundaryNodeIds, double? Latitude, double? Longitude);

    // --- element read/write ---

    private async Task<ContactPointModel> ToContactPointAsync(ContentItem element, CancellationToken cancellationToken)
    {
        var part = PartiesConstants.ContentTypes.ContactPoint;
        return new ContactPointModel(
            element.ContentItemId,
            await optionKeys.KeyForIdAsync(PartiesOptionSets.ContactPointKind, PickerId(element, part, "Kind"), cancellationToken),
            Text(element, part, "Value") ?? string.Empty,
            Text(element, part, "Label"),
            Flag(element, part, "Preferred"),
            await optionKeys.KeyForIdAsync(GlobalPhoneCountryCodes, PickerId(element, part, "PhoneCountry"), cancellationToken));
    }

    private async Task<AddressModel> ToAddressAsync(ContentItem element, CancellationToken cancellationToken)
    {
        var part = PartiesConstants.ContentTypes.Address;
        var geoField = element.Get<ContentPart>(part)?.Get<GeoStackField>("Geo") ?? new GeoStackField();
        return new AddressModel(
            element.ContentItemId,
            await optionKeys.KeyForIdAsync(PartiesOptionSets.AddressKind, PickerId(element, part, "Kind"), cancellationToken),
            Text(element, part, "Line1"),
            Text(element, part, "Line2"),
            Text(element, part, "Locality"),
            Text(element, part, "PostalCode"),
            geoField.NodeAt(1),
            geoField.Stack.ToDictionary(entry => entry.Level, entry => entry.NodeId),
            geoField.BoundaryNodeIds,
            geoField.Latitude,
            geoField.Longitude,
            Flag(element, part, "Preferred"));
    }

    private static void ApplyContactPoint(ContentItem element, ContactPointWriteModel write, string kindId, string? phoneCountryId)
    {
        var part = PartiesConstants.ContentTypes.ContactPoint;
        element.DisplayText = write.Value.Trim();
        SetPicker(element, part, "Kind", PartiesOptionSets.ContactPointKind, kindId);
        SetText(element, part, "Value", write.Value.Trim());
        SetText(element, part, "Label", write.Label);
        SetFlag(element, part, "Preferred", write.Preferred);
        SetPicker(element, part, "PhoneCountry", GlobalPhoneCountryCodes, phoneCountryId);
    }

    private static void ApplyAddress(ContentItem element, AddressWriteModel write, ResolvedAddress resolved)
    {
        var part = PartiesConstants.ContentTypes.Address;
        SetPicker(element, part, "Kind", PartiesOptionSets.AddressKind, resolved.KindId);
        SetText(element, part, "Line1", write.Line1);
        SetText(element, part, "Line2", write.Line2);
        SetText(element, part, "Locality", write.Locality);
        SetText(element, part, "PostalCode", write.PostalCode);
        element.Alter<ContentPart>(part, p => p.Alter<GeoStackField>("Geo", field =>
        {
            field.Stack = resolved.Stack.Nodes.Select(node => new GeoStackEntry { Level = node.Level, NodeId = node.NodeId }).ToList();
            // Re-resolved on every write: a re-entered address keeps nothing from the old
            // text, since neither the point nor the boundaries need still hold.
            field.BoundaryNodeIds = [.. resolved.BoundaryNodeIds];
            field.Latitude = resolved.Latitude;
            field.Longitude = resolved.Longitude;
        }));
        SetFlag(element, part, "Preferred", write.Preferred);
        element.DisplayText = string.Join(", ", new[] { write.Line1, write.Locality }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void AppendElement(ContentItem party, string bagName, ContentItem element, bool preferred, string kindId, string elementPart)
    {
        party.Alter<BagPart>(bagName, bag =>
        {
            if (preferred)
            {
                ClearOtherPreferred(bag, element.ContentItemId, kindId, elementPart);
            }

            bag.ContentItems.Add(element);
        });
    }

    private async Task<bool> RemoveElementAsync(ContentItem party, string bagName, string elementId)
    {
        var removed = false;
        party.Alter<BagPart>(bagName, bag => removed = bag.ContentItems.RemoveAll(item => item.ContentItemId == elementId) > 0);

        if (removed)
        {
            await contentManager.UpdateAsync(party);
        }

        return removed;
    }

    // One preferred entry per kind: marking one preferred clears the rest of that kind.
    private static void ClearOtherPreferred(BagPart bag, string exceptId, string kindId, string elementPart)
    {
        foreach (var sibling in bag.ContentItems)
        {
            if (sibling.ContentItemId != exceptId && PickerId(sibling, elementPart, "Kind") == kindId)
            {
                SetFlag(sibling, elementPart, "Preferred", false);
            }
        }
    }

    private static IReadOnlyList<ContentItem> Elements(ContentItem party, string bagName) =>
        party.Get<BagPart>(bagName)?.ContentItems ?? [];

    private static string? Text(ContentItem element, string part, string field) =>
        element.Get<ContentPart>(part)?.Get<TextField>(field)?.Text;

    private static bool Flag(ContentItem element, string part, string field) =>
        element.Get<ContentPart>(part)?.Get<BooleanField>(field)?.Value ?? false;

    private static string? PickerId(ContentItem element, string part, string field) =>
        element.Get<ContentPart>(part)?.Get<OptionPickerField>(field)?.SelectedIds.FirstOrDefault();

    private static void SetText(ContentItem element, string part, string field, string? value) =>
        element.Alter<ContentPart>(part, p => p.Alter<TextField>(field, f => f.Text = string.IsNullOrWhiteSpace(value) ? null : value.Trim()));

    private static void SetFlag(ContentItem element, string part, string field, bool value) =>
        element.Alter<ContentPart>(part, p => p.Alter<BooleanField>(field, f => f.Value = value));

    private static void SetPicker(ContentItem element, string part, string field, string listKey, string? optionId) =>
        element.Alter<ContentPart>(part, p => p.Alter<OptionPickerField>(field, f =>
        {
            f.SourceKey = CrestOptionSourceKeys.ForContentPartList(listKey);
            f.SelectedIds = optionId is null ? [] : [optionId];
        }));
}
