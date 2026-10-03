using Crest.Regions.Indexes;
using Crest.Regions.Models;
using OrchardCore.ContentManagement;
using YesSql;

namespace Crest.Regions.Services;

public sealed class LocalizationProfileResolver(ISession session, IContentManager contentManager) : ILocalizationProfileResolver
{
    public async Task<LocalizationProfileModel?> ResolveAsync(ContentItem? carrier, CancellationToken cancellationToken = default)
    {
        var id = carrier?.As<CrestLocalizationProfileReferencePart>()?.LocalizationProfileId;
        return string.IsNullOrWhiteSpace(id) ? null : await GetAsync(id, cancellationToken);
    }

    public async Task<LocalizationProfileModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default)
    {
        var item = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        return item is null ? null : ToModel(item);
    }

    public async Task<LocalizationProfileModel?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim();
        var item = await session
            .Query<ContentItem, LocalizationProfileIndex>(index => index.Key == normalized && index.Published)
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToModel(item);
    }

    public async Task<IReadOnlyList<LocalizationProfileModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session
            .Query<ContentItem, LocalizationProfileIndex>(index => index.Published)
            .ListAsync(cancellationToken);
        return items.Select(ToModel).OrderBy(context => context.DisplayText, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static LocalizationProfileModel ToModel(ContentItem item)
    {
        var part = item.As<CrestLocalizationProfilePart>() ?? new CrestLocalizationProfilePart();
        return new LocalizationProfileModel(
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
