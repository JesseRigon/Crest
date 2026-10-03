using Crest.Parties.Constants;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql.Indexes;

namespace Crest.Parties.Indexes;

/// <summary>
/// One row per position a Person holds. Positions are bag-contained (inside the
/// Person document), and stock field indexes do not descend into bags, so "who holds
/// a position in organization X" needs this map index to be a query rather than a
/// scan of every person.
/// </summary>
public sealed class PartyPositionIndex : MapIndex
{
    /// <summary>The Person content item.</summary>
    public string ContentItemId { get; set; } = string.Empty;

    /// <summary>The contained OrgPosition item's id.</summary>
    public string PositionId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public string? Title { get; set; }

    public bool IsPrimary { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}

public sealed class PartyPositionIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<PartyPositionIndex>()
            .Map(contentItem =>
            {
                // Same rule as stock field index providers: soft-deleted items (neither
                // published nor latest) drop out of the index.
                if (contentItem.ContentType != PartiesConstants.ContentTypes.Person || (!contentItem.Published && !contentItem.Latest))
                {
                    return [];
                }

                var positions = contentItem.Get<BagPart>(PartiesConstants.Bags.Positions)?.ContentItems ?? [];

                return positions
                    .Select(position =>
                    {
                        var part = position.Get<ContentPart>(PartiesConstants.ContentTypes.OrgPosition);
                        var organizationId = part?.Get<ContentPickerField>("Organization")?.ContentItemIds.FirstOrDefault();
                        return organizationId is null
                            ? null
                            : new PartyPositionIndex
                            {
                                ContentItemId = contentItem.ContentItemId,
                                PositionId = position.ContentItemId,
                                OrganizationId = organizationId,
                                Title = part?.Get<TextField>("Title")?.Text,
                                IsPrimary = part?.Get<BooleanField>("Primary")?.Value ?? false,
                                Published = contentItem.Published,
                                Latest = contentItem.Latest,
                            };
                    })
                    .Where(row => row is not null)
                    .Select(row => row!)
                    .ToArray();
            });
    }
}
