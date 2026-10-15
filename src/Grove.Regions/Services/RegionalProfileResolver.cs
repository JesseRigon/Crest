using Grove.Regions.Indexes;
using Grove.Regions.Models;
using Grove.ContentManagement;
using YesSql;

namespace Grove.Regions.Services;

public sealed class RegionalProfileResolver(ISession session, IContentManager contentManager) : IRegionalProfileResolver
{
    public async Task<RegionalProfileModel?> ResolveAsync(ContentItem? carrier, CancellationToken cancellationToken = default)
    {
        var id = carrier?.Get<GroveRegionalProfileReferencePart>(typeof(GroveRegionalProfileReferencePart).Name)?.RegionalProfileId;
        return string.IsNullOrWhiteSpace(id) ? null : await GetAsync(id, cancellationToken);
    }

    public async Task<RegionalProfileModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        return item is null ? null : ToModel(item);
    }

    public async Task<RegionalProfileModel?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim();
        var item = await session
            .Query<ContentItem, RegionalProfileIndex>(index => index.Key == normalized && index.Published)
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToModel(item);
    }

    public async Task<IReadOnlyList<RegionalProfileModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session
            .Query<ContentItem, RegionalProfileIndex>(index => index.Published)
            .ListAsync(cancellationToken);
        return items.Select(ToModel).OrderBy(context => context.DisplayText, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static RegionalProfileModel ToModel(ContentItem item)
    {
        var part = item.Get<GroveRegionalProfilePart>(typeof(GroveRegionalProfilePart).Name) ?? new GroveRegionalProfilePart();
        return new RegionalProfileModel(
            item.ContentItemId,
            part.Key,
            item.DisplayText ?? part.Key,
            part.DefaultCountry,
            part.MeasurementSystem,
            part.TimeZone,
            part.Language,
            part.Enabled);
    }
}
