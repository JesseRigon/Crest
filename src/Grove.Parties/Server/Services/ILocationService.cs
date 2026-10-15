using Grove.Access;
using Grove.Data.Scoping;
using Grove.Regions.Fields;
using Grove.Parties.Constants;
using Grove.Parties.Indexes;
using Grove.Parties.ViewModels;
using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.Flows.Models;
using YesSql;

namespace Grove.Parties.Services;

public interface ILocationService
{
    Task<IReadOnlyList<LocationModel>> ListAsync(CancellationToken cancellationToken = default);
    Task<LocationModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default);
    /// <summary>The tenant's home Location, or null when none is marked primary.</summary>
    Task<LocationModel?> GetPrimaryAsync(CancellationToken cancellationToken = default);
    /// <summary>Marks one Location primary and clears the flag on every other.</summary>
    Task SetPrimaryAsync(string contentItemId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Locations, read through the caller's scope on <see cref="LocationIndex"/> (docs/access.md,
/// Domains onto the extension system): a list or a lookup returns only the Locations the caller
/// may see. The tenant's home Location (<see cref="GetPrimaryAsync"/>) is configuration every
/// posting reads, and stays unscoped.
/// </summary>
public sealed class LocationService(ISession session, IContentManager contentManager, IPartyContactsService contacts, ICallerContextAccessor callers, IScopeSetProvider scopes) : ILocationService
{
    public async Task<IReadOnlyList<LocationModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(cancellationToken);
        if (scope.Kind == ScopeKind.None)
        {
            return [];
        }

        var items = await session.Query<ContentItem, LocationIndex>(index => index.Published).Where(ScopeExpressions.ToPredicate<LocationIndex>(scope)).ListAsync(cancellationToken);
        var models = new List<LocationModel>();
        foreach (var item in items)
        {
            models.Add(await ToModelAsync(item, cancellationToken));
        }

        return models.OrderByDescending(location => location.IsPrimary).ThenBy(location => location.DisplayText, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<LocationModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(cancellationToken);
        if (scope.Kind == ScopeKind.None)
        {
            return null;
        }

        var item = await session.Query<ContentItem, LocationIndex>(index => index.ContentItemId == contentItemId && index.Published)
            .Where(ScopeExpressions.ToPredicate<LocationIndex>(scope))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null || item.ContentType != PartiesConstants.ContentTypes.Location ? null : await ToModelAsync(item, cancellationToken);
    }

    /// <summary>The caller's rule for <see cref="LocationIndex"/>; no caller reads nothing.</summary>
    private async Task<ScopeRule> ScopeAsync(CancellationToken cancellationToken) =>
        callers.Current is { } caller ? (await scopes.GetAsync(caller, cancellationToken)).Require(nameof(LocationIndex)) : ScopeRule.None;

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
