using System.Globalization;
using Grove.Contents.Localization.Models;
using Grove.Contents.Localization.Records;
using Grove.Contents.Management;
using Grove.Contents.Services;
using Grove.Data.Localization;
using YesSql;

namespace Grove.Contents.Localization.Services;

/// <summary>
/// The content items list's culture filter (<c>?culture=</c>): the items whose localization part
/// has that culture; offered with the tenant's supported cultures.
/// </summary>
public sealed class LocalizationPartContentItemListFilter(ILocalizationService localizationService) : IContentItemListFilter
{
    public const string ParameterName = "culture";

    public async Task<IReadOnlyList<ContentItemListFilterDescriptor>> DescribeAsync(string contentType, CancellationToken cancellationToken = default)
    {
        var options = (await localizationService.GetSupportedCulturesAsync())
            .Select(culture => new ContentItemListFilterOption(culture, DisplayName(culture)))
            .ToArray();

        return [new ContentItemListFilterDescriptor(ParameterName, "Culture", options, nameof(LocalizationPart))];
    }

    public Task FilterAsync(IQuery<ContentItem> query, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        if (parameters.TryGetValue(ParameterName, out var culture) && !string.IsNullOrWhiteSpace(culture))
        {
            var normalized = culture.Trim().ToLowerInvariant();
            query.With<LocalizedContentItemIndex>(index => (index.Published || index.Latest) && index.Culture == normalized);
        }

        return Task.CompletedTask;
    }

    private static string DisplayName(string culture)
    {
        try
        {
            return CultureInfo.GetCultureInfo(culture).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }
}
