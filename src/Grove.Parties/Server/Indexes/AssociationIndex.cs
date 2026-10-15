using Grove.ContentFields.Fields;
using Grove.ContentManagement;
using Grove.Fields;
using Grove.Parties.Constants;
using YesSql.Indexes;

namespace Grove.Parties.Indexes;

/// <summary>
/// One row per Association: its two ends (each a person and the organization they act
/// in) and its type, so "my connections" is a query. Associations are content items,
/// kept apart from the reports-to hierarchy so its root guard and closure table never
/// see them (ruling 2026-10-06).
/// </summary>
public sealed class AssociationIndex : MapIndex
{
    public string ContentItemId { get; set; } = string.Empty;
    public string? TypeId { get; set; }
    public string? FromPersonId { get; set; }
    public string? FromOrganizationId { get; set; }
    public string? ToPersonId { get; set; }
    public string? ToOrganizationId { get; set; }
    public bool Published { get; set; }
    public bool Latest { get; set; }
}

public sealed class AssociationIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<AssociationIndex>()
            .Map(contentItem =>
            {
                if (contentItem.ContentType != PartiesConstants.ContentTypes.Association || (!contentItem.Published && !contentItem.Latest))
                {
                    return null;
                }

                var part = contentItem.Get<ContentPart>(PartiesConstants.ContentTypes.Association);
                return new AssociationIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    TypeId = part?.Get<OptionPickerField>(AssociationFields.Type)?.SelectedIds.FirstOrDefault(),
                    FromPersonId = First(part, AssociationFields.FromPerson),
                    FromOrganizationId = First(part, AssociationFields.FromOrganization),
                    ToPersonId = First(part, AssociationFields.ToPerson),
                    ToOrganizationId = First(part, AssociationFields.ToOrganization),
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }

    private static string? First(ContentPart? part, string field) =>
        part?.Get<OptionPickerField>(field)?.SelectedIds.FirstOrDefault();
}

/// <summary>Field names on the Association part.</summary>
public static class AssociationFields
{
    public const string Type = "Type";
    public const string FromPerson = "FromPerson";
    public const string FromOrganization = "FromOrganization";
    public const string ToPerson = "ToPerson";
    public const string ToOrganization = "ToOrganization";
}
