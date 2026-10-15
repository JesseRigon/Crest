using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.Fields;
using Grove.Flows.Models;
using Grove.Parties.Constants;
using YesSql.Indexes;

namespace Grove.Parties.Indexes;

/// <summary>
/// One row per contact point on a party. Stock field indexes do not look inside bags, so
/// every queryable bag facet gets its own index (ruling 2026-10-06). A contact kind's
/// TYPE (phone, e-mail, web, ...) is the kind option's Category: a query by type resolves
/// the type to its kind ids from the list (a small, per-request document) and filters on
/// <see cref="KindId"/>, so a recategorized kind needs no reindex.
/// </summary>
public sealed class ContactPointIndex : MapIndex
{
    public string PartyId { get; set; } = string.Empty;
    public string PartyContentType { get; set; } = string.Empty;
    public string ContactPointId { get; set; } = string.Empty;
    public string? KindId { get; set; }
    public string? Value { get; set; }
    public bool Preferred { get; set; }
    public bool Published { get; set; }
    public bool Latest { get; set; }
}

/// <summary>One row per legal entity name of an organization: "who trades as X" and
/// "the legal name of Y" are queries.</summary>
public sealed class OrganizationNameIndex : MapIndex
{
    public string OrganizationId { get; set; } = string.Empty;
    public string NameId { get; set; } = string.Empty;
    public string? KindId { get; set; }
    public string? Name { get; set; }
    public bool Primary { get; set; }
    public bool Published { get; set; }
    public bool Latest { get; set; }
}

public sealed class ContactPointIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<ContactPointIndex>()
            .Map(contentItem =>
            {
                if (!contentItem.Published && !contentItem.Latest)
                {
                    return [];
                }

                var bag = contentItem.Get<BagPart>(PartiesConstants.Bags.ContactPoints);
                if (bag is null)
                {
                    return [];
                }

                const string Part = PartiesConstants.ContentTypes.ContactPoint;
                return bag.ContentItems.Select(element =>
                {
                    var part = element.Get<ContentPart>(Part);
                    return new ContactPointIndex
                    {
                        PartyId = contentItem.ContentItemId,
                        PartyContentType = contentItem.ContentType,
                        ContactPointId = element.ContentItemId,
                        KindId = part?.Get<OptionPickerField>("Kind")?.SelectedIds.FirstOrDefault(),
                        Value = Truncate(part?.Get<TextField>("Value")?.Text, 255),
                        Preferred = part?.Get<BooleanField>("Preferred")?.Value ?? false,
                        Published = contentItem.Published,
                        Latest = contentItem.Latest,
                    };
                }).ToArray();
            });
    }

    internal static string? Truncate(string? value, int length) =>
        value is null || value.Length <= length ? value : value[..length];
}

public sealed class OrganizationNameIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<OrganizationNameIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != PartiesConstants.ContentTypes.Organization || (!contentItem.Published && !contentItem.Latest))
                {
                    return [];
                }

                var bag = contentItem.Get<BagPart>(PartiesConstants.Bags.OrganizationNames);
                if (bag is null)
                {
                    return [];
                }

                const string Part = PartiesConstants.ContentTypes.OrganizationName;
                return bag.ContentItems.Select(element =>
                {
                    var part = element.Get<ContentPart>(Part);
                    return new OrganizationNameIndex
                    {
                        OrganizationId = contentItem.ContentItemId,
                        NameId = element.ContentItemId,
                        KindId = part?.Get<OptionPickerField>("Kind")?.SelectedIds.FirstOrDefault(),
                        Name = ContactPointIndexProvider.Truncate(part?.Get<TextField>("Name")?.Text, 255),
                        Primary = part?.Get<BooleanField>("Primary")?.Value ?? false,
                        Published = contentItem.Published,
                        Latest = contentItem.Latest,
                    };
                }).ToArray();
            });
    }
}
