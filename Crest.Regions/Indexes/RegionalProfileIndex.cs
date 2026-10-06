using Crest.Regions.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace Crest.Regions.Indexes;

public sealed class RegionalProfileIndex : MapIndex
{
    public string ContentItemId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? DefaultCountry { get; set; }
    public bool Enabled { get; set; }
    public bool Published { get; set; }
    public bool Latest { get; set; }
}

public sealed class RegionalProfileIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<RegionalProfileIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != RegionsConstants.ContentTypes.RegionalProfile || (!contentItem.Published && !contentItem.Latest))
                {
                    return null;
                }

                var part = contentItem.Get<CrestRegionalProfilePart>(typeof(CrestRegionalProfilePart).Name);
                if (part is null)
                {
                    return null;
                }

                return new RegionalProfileIndex
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
