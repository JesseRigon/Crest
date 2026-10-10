using Crest.Regions.Fields;
using Crest.Parties.Constants;
using Crest.Parties.Indexes;
using Crest.Parties.ViewModels;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.Flows.Models;
using YesSql;

namespace Crest.Parties.Services;

public interface ILocationService
{
    Task<IReadOnlyList<LocationModel>> ListAsync(CancellationToken cancellationToken = default);
    Task<LocationModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default);
    /// <summary>The tenant's home Location, or null when none is marked primary.</summary>
    Task<LocationModel?> GetPrimaryAsync(CancellationToken cancellationToken = default);
    /// <summary>Marks one Location primary and clears the flag on every other.</summary>
    Task SetPrimaryAsync(string contentItemId, CancellationToken cancellationToken = default);
}

public sealed class LocationService(ISession session, IContentManager contentManager, IPartyContactsService contacts) : ILocationService
{
    public async Task<IReadOnlyList<LocationModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session.Query<ContentItem, LocationIndex>(index => index.Published).ListAsync(cancellationToken);
        var models = new List<LocationModel>();
        foreach (var item in items)
        {
            models.Add(await ToModelAsync(item, cancellationToken));
        }

        return models.OrderByDescending(location => location.IsPrimary).ThenBy(location => location.DisplayText, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<LocationModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        return item is null || item.ContentType != PartiesConstants.ContentTypes.Location ? null : await ToModelAsync(item, cancellationToken);
    }

    public async Task<LocationModel?> GetPrimaryAsync(CancellationToken cancellationToken = default)
    {
        var item = await session.Query<ContentItem, LocationIndex>(index => index.IsPrimary && index.Published).FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : await ToModelAsync(item, cancellationToken);
    }

    public async Task SetPrimaryAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        var items = await session.Query<ContentItem, LocationIndex>(index => index.Latest).ListAsync(cancellationToken);
        foreach (var item in items)
        {
            var wanted = item.ContentItemId == contentItemId;
            var current = item.Get<ContentPart>(PartiesConstants.ContentTypes.Location)?.Get<BooleanField>("IsPrimary")?.Value ?? false;
            if (wanted == current)
            {
                continue;
            }

            item.Alter<ContentPart>(PartiesConstants.ContentTypes.Location, part => part.Alter<BooleanField>("IsPrimary", field => field.Value = wanted));
            await contentManager.UpdateAsync(item);
            await contentManager.PublishAsync(item);
        }
    }

    private async Task<LocationModel> ToModelAsync(ContentItem item, CancellationToken cancellationToken)
    {
        var part = item.Get<ContentPart>(PartiesConstants.ContentTypes.Location);
        var contactsModel = await contacts.ReadAsync(item, cancellationToken);
        var points = (item.Get<BagPart>(PartiesConstants.Bags.Points)?.ContentItems ?? [])
            .Select(ToPoint)
            .ToArray();

        return new LocationModel(
            item.ContentItemId,
            item.DisplayText ?? string.Empty,
            part?.Get<BooleanField>("IsPrimary")?.Value ?? false,
            part?.Get<NumericField>("FloorArea")?.Value,
            (int?)part?.Get<NumericField>("Storeys")?.Value,
            contactsModel.Addresses,
            points);
    }

    private static LocationPointModel ToPoint(ContentItem element)
    {
        var part = element.Get<ContentPart>(PartiesConstants.ContentTypes.LocationPoint);
        var geo = part?.Get<GeoStackField>("Geo") ?? new GeoStackField();
        return new LocationPointModel(
            element.ContentItemId,
            part?.Get<TextField>("Label")?.Text ?? element.DisplayText ?? string.Empty,
            geo.NodeAt(1),
            geo.Stack.ToDictionary(entry => entry.Level, entry => entry.NodeId),
            geo.Latitude,
            geo.Longitude,
            part?.Get<NumericField>("FloorArea")?.Value,
            (int?)part?.Get<NumericField>("Storey")?.Value);
    }
}
