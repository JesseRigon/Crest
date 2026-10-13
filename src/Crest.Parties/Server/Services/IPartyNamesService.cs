using Crest.ContentManagement;
using Crest.Parties.Constants;
using Crest.Parties.ViewModels;

namespace Crest.Parties.Services;

/// <summary>
/// An organization's legal entity names (the OrganizationNames bag): legal names,
/// trade names (DBA), former names, each with a kind and one primary entry per kind.
/// A superseded name becomes the <c>former</c> kind; there are no effective dates
/// (ruling 2026-10-05). Callers hand in an Organization they have loaded and
/// authorized; this never authorizes.
/// </summary>
public interface IPartyNamesService
{
    Task<IReadOnlyList<OrganizationNameModel>> ReadAsync(ContentItem organization, CancellationToken cancellationToken = default);

    Task<OrganizationNameModel> AddAsync(ContentItem organization, OrganizationNameWriteModel write, CancellationToken cancellationToken = default);

    /// <summary>Null when no name with that id is on the organization.</summary>
    Task<OrganizationNameModel?> UpdateAsync(ContentItem organization, string nameId, OrganizationNameWriteModel write, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(ContentItem organization, string nameId, CancellationToken cancellationToken = default);
}

public sealed class PartyNamesService(PartyBagElementService bags) : IPartyNamesService
{
    private const string Part = PartiesConstants.ContentTypes.OrganizationName;

    public async Task<IReadOnlyList<OrganizationNameModel>> ReadAsync(ContentItem organization, CancellationToken cancellationToken = default)
    {
        var names = new List<OrganizationNameModel>();
        foreach (var element in bags.Elements(organization, PartyBags.OrganizationNames))
        {
            names.Add(await ToModelAsync(element, cancellationToken));
        }

        return names;
    }

    public async Task<OrganizationNameModel> AddAsync(ContentItem organization, OrganizationNameWriteModel write, CancellationToken cancellationToken = default)
    {
        var kindId = await ResolveAsync(write, cancellationToken);
        var element = await bags.AddAsync(organization, PartyBags.OrganizationNames, kindId, write.Primary, item => Apply(item, write));
        return await ToModelAsync(element, cancellationToken);
    }

    public async Task<OrganizationNameModel?> UpdateAsync(ContentItem organization, string nameId, OrganizationNameWriteModel write, CancellationToken cancellationToken = default)
    {
        var kindId = await ResolveAsync(write, cancellationToken);
        var updated = await bags.UpdateAsync(organization, PartyBags.OrganizationNames, nameId, kindId, write.Primary, item => Apply(item, write));
        return updated is null ? null : await ToModelAsync(updated, cancellationToken);
    }

    public Task<bool> RemoveAsync(ContentItem organization, string nameId, CancellationToken cancellationToken = default)
        => bags.RemoveAsync(organization, PartyBags.OrganizationNames, nameId);

    private async Task<string> ResolveAsync(OrganizationNameWriteModel write, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(write.Name))
        {
            throw new InvalidOperationException("A legal entity name needs a name.");
        }

        return await bags.RequireKindIdAsync(PartyBags.OrganizationNames, write.Kind, cancellationToken);
    }

    private static void Apply(ContentItem element, OrganizationNameWriteModel write)
    {
        element.DisplayText = write.Name.Trim();
        PartyBagElementService.SetText(element, Part, "Name", write.Name);
    }

    private async Task<OrganizationNameModel> ToModelAsync(ContentItem element, CancellationToken cancellationToken) => new(
        element.ContentItemId,
        await bags.KindKeyAsync(element, PartyBags.OrganizationNames, cancellationToken),
        PartyBagElementService.Text(element, Part, "Name") ?? string.Empty,
        PartyBagElementService.Flag(element, Part, "Primary"));
}
