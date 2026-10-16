using Grove.Parties.Constants;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using YesSql.Indexes;

namespace Grove.Parties.Indexes;

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
                    IsPrimary = contentItem.Get<ContentFieldSet>(PartiesConstants.ContentTypes.Location)?.Get<BooleanField>("IsPrimary")?.Value ?? false,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }
}
