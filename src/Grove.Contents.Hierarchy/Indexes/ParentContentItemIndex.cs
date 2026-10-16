using Grove.Contents.Management;
using Grove.Contents.Hierarchy.Models;
using YesSql.Indexes;

namespace Grove.Contents.Hierarchy.Indexes;

public class ParentContentItemIndex : MapIndex
{
    public const int MaxDisplayTextSize = 255;

    public string ListContentItemId { get; set; }

    public int Order { get; set; }

    public string ContentItemId { get; set; }

    public string ListContentType { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }

    public string DisplayText { get; set; }
}

public class ParentContentItemIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<ParentContentItemIndex>()
            .Map(contentItem =>
            {
                if (!contentItem.Latest && !contentItem.Published)
                {
                    return null;
                }

                if (!contentItem.TryGet<ParentContentItemFieldSet>(out var parentContentItemFieldSet))
                {
                    return null;
                }

                var parentContentItemIndex = new ParentContentItemIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    ListContentType = parentContentItemFieldSet.ListContentType,
                    ListContentItemId = parentContentItemFieldSet.ListContentItemId,
                    Order = parentContentItemFieldSet.Order,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };

                if (contentItem.DisplayText?.Length > ParentContentItemIndex.MaxDisplayTextSize)
                {
                    parentContentItemIndex.DisplayText = contentItem.DisplayText[..ParentContentItemIndex.MaxDisplayTextSize];
                }
                else
                {
                    parentContentItemIndex.DisplayText = contentItem.DisplayText;
                }

                return parentContentItemIndex;
            });
    }
}
