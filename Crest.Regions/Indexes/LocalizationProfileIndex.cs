using Crest.Regions.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace Crest.Regions.Indexes;

public sealed class LocalizationProfileIndex : MapIndex
{
    public string ContentItemId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? DefaultCountry { get; set; }
    public bool Enabled { get; set; }
    public bool Published { get; set; }
    public bool Latest { get; set; }
}

public sealed class LocalizationProfileIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<LocalizationProfileIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != RegionsConstants.ContentTypes.LocalizationProfile || (!contentItem.Published && !contentItem.Latest))
                {
                    return null;
                }

                var part = contentItem.As<CrestLocalizationProfilePart>();
                if (part is null)
                {
                    return null;
                }

                return new LocalizationProfileIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    Key = part.Key,
                    DefaultCountry = part.DefaultCountry,
                    Enabled = part.Enabled,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }
}
