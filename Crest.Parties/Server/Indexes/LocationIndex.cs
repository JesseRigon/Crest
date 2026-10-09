using Crest.Parties.Constants;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using YesSql.Indexes;

namespace Crest.Parties.Indexes;

public sealed class LocationIndex : MapIndex
{
    public string ContentItemId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool Published { get; set; }
    public bool Latest { get; set; }
}

public sealed class LocationIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<LocationIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != PartiesConstants.ContentTypes.Location || (!contentItem.Published && !contentItem.Latest))
                {
                    return null;
                }

                return new LocationIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    IsPrimary = contentItem.Get<ContentPart>(PartiesConstants.ContentTypes.Location)?.Get<BooleanField>("IsPrimary")?.Value ?? false,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }
}
