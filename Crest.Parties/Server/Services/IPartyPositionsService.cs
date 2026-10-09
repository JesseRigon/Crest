using Crest.Parties.Constants;
using Crest.Parties.Indexes;
using Crest.Parties.ViewModels;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.Flows.Models;
using YesSql;

namespace Crest.Parties.Services;

/// <summary>
/// The positions a Person holds in Organizations (the Positions bag on Person) and
/// the organization-side lookup of who holds them. Like the contacts service, callers
/// hand in a party they have loaded AND authorized; this never authorizes.
/// </summary>
public interface IPartyPositionsService
{
    Task<IReadOnlyList<PositionModel>> ReadAsync(ContentItem person, CancellationToken cancellationToken = default);

    Task<PositionModel> AddAsync(ContentItem person, PositionWriteModel write, CancellationToken cancellationToken = default);

    Task<PositionModel?> UpdateAsync(ContentItem person, string positionId, PositionWriteModel write, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(ContentItem person, string positionId, CancellationToken cancellationToken = default);

    /// <summary>Everyone holding a position in <paramref name="organizationId"/>, via
    /// the position index (latest versions).</summary>
    Task<IReadOnlyList<OrganizationPersonModel>> ListPeopleAsync(string organizationId, CancellationToken cancellationToken = default);
}

public sealed class PartyPositionsService(IContentManager contentManager, ISession session) : IPartyPositionsService
{
    private const string Part = PartiesConstants.ContentTypes.OrgPosition;

    public async Task<IReadOnlyList<PositionModel>> ReadAsync(ContentItem person, CancellationToken cancellationToken = default)
    {
        var elements = Elements(person);
        var organizationNames = await OrganizationNamesAsync(elements.Select(OrganizationId).Where(id => id is not null)!);

        return elements
            .Select(element => ToModel(element, organizationNames))
            .ToArray();
    }

    public async Task<PositionModel> AddAsync(ContentItem person, PositionWriteModel write, CancellationToken cancellationToken = default)
    {
        var organization = await RequireOrganizationAsync(write.OrganizationId);
        var element = await contentManager.NewAsync(PartiesConstants.ContentTypes.OrgPosition);
        Apply(element, write, organization);

        person.Alter<BagPart>(PartiesConstants.Bags.Positions, bag =>
        {
            if (write.Primary)
            {
                ClearOtherPrimary(bag, element.ContentItemId);
            }

            bag.ContentItems.Add(element);
        });
        await contentManager.UpdateAsync(person);

        return ToModel(element, new Dictionary<string, string> { [organization.ContentItemId] = organization.DisplayText });
    }

    public async Task<PositionModel?> UpdateAsync(ContentItem person, string positionId, PositionWriteModel write, CancellationToken cancellationToken = default)
    {
        var organization = await RequireOrganizationAsync(write.OrganizationId);
        ContentItem? updated = null;

        person.Alter<BagPart>(PartiesConstants.Bags.Positions, bag =>
        {
            updated = bag.ContentItems.FirstOrDefault(item => item.ContentItemId == positionId);
            if (updated is null)
            {
                return;
            }

            Apply(updated, write, organization);
            if (write.Primary)
            {
                ClearOtherPrimary(bag, updated.ContentItemId);
            }
        });

        if (updated is null)
        {
            return null;
        }

        await contentManager.UpdateAsync(person);
        return ToModel(updated, new Dictionary<string, string> { [organization.ContentItemId] = organization.DisplayText });
    }

    public async Task<bool> RemoveAsync(ContentItem person, string positionId, CancellationToken cancellationToken = default)
    {
        var removed = false;
        person.Alter<BagPart>(PartiesConstants.Bags.Positions, bag => removed = bag.ContentItems.RemoveAll(item => item.ContentItemId == positionId) > 0);

        if (removed)
        {
            await contentManager.UpdateAsync(person);
        }

        return removed;
    }

    public async Task<IReadOnlyList<OrganizationPersonModel>> ListPeopleAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var rows = await session.QueryIndex<PartyPositionIndex>(index => index.OrganizationId == organizationId && index.Latest)
            .ListAsync(cancellationToken);
        var rowList = rows.ToArray();
        if (rowList.Length == 0)
        {
            return [];
        }

        var people = (await contentManager.GetAsync(rowList.Select(row => row.ContentItemId).Distinct().ToArray(), VersionOptions.Latest))
            .Where(item => item is not null)
            .ToDictionary(item => item.ContentItemId, item => item.DisplayText, StringComparer.OrdinalIgnoreCase);

        return rowList
            .Select(row => new OrganizationPersonModel(
                row.ContentItemId,
                people.TryGetValue(row.ContentItemId, out var name) ? name : string.Empty,
                row.PositionId,
                row.Title,
                Department(row),
                row.IsPrimary))
            .OrderByDescending(entry => entry.Primary)
            .ThenBy(entry => entry.PersonName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Department is not indexed (it is not a lookup axis); the org-side list shows
        // the indexed columns only.
        static string? Department(PartyPositionIndex _) => null;
    }

    private async Task<ContentItem> RequireOrganizationAsync(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new InvalidOperationException("A position needs an organization.");
        }

        var organization = await contentManager.GetAsync(organizationId, VersionOptions.Latest);
        if (organization is not { ContentType: PartiesConstants.ContentTypes.Organization })
        {
            throw new InvalidOperationException($"'{organizationId}' is not an Organization content item.");
        }

        return organization;
    }

    private async Task<Dictionary<string, string>> OrganizationNamesAsync(IEnumerable<string> organizationIds)
    {
        var ids = organizationIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return (await contentManager.GetAsync(ids, VersionOptions.Latest))
            .Where(item => item is not null)
            .ToDictionary(item => item.ContentItemId, item => item.DisplayText, StringComparer.OrdinalIgnoreCase);
    }

    private static void Apply(ContentItem element, PositionWriteModel write, ContentItem organization)
    {
        element.DisplayText = string.Join(" @ ", new[] { write.Title, organization.DisplayText }.Where(value => !string.IsNullOrWhiteSpace(value)));
        element.Alter<ContentPart>(Part, part =>
        {
            part.Alter<ContentPickerField>("Organization", field => field.ContentItemIds = [organization.ContentItemId]);
            part.Alter<TextField>("Title", field => field.Text = Clean(write.Title));
            part.Alter<TextField>("Department", field => field.Text = Clean(write.Department));
            part.Alter<BooleanField>("Primary", field => field.Value = write.Primary);
        });
    }

    // One primary position per person.
    private static void ClearOtherPrimary(BagPart bag, string exceptId)
    {
        foreach (var sibling in bag.ContentItems.Where(item => item.ContentItemId != exceptId))
        {
            sibling.Alter<ContentPart>(Part, part => part.Alter<BooleanField>("Primary", field => field.Value = false));
        }
    }

    private static PositionModel ToModel(ContentItem element, IReadOnlyDictionary<string, string> organizationNames)
    {
        var part = element.Get<ContentPart>(Part);
        var organizationId = OrganizationId(element) ?? string.Empty;

        return new PositionModel(
            element.ContentItemId,
            organizationId,
            organizationNames.TryGetValue(organizationId, out var name) ? name : null,
            part?.Get<TextField>("Title")?.Text,
            part?.Get<TextField>("Department")?.Text,
            part?.Get<BooleanField>("Primary")?.Value ?? false);
    }

    private static IReadOnlyList<ContentItem> Elements(ContentItem person) =>
        person.Get<BagPart>(PartiesConstants.Bags.Positions)?.ContentItems ?? [];

    private static string? OrganizationId(ContentItem element) =>
        element.Get<ContentPart>(Part)?.Get<ContentPickerField>("Organization")?.ContentItemIds.FirstOrDefault();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
