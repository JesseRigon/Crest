using Crest.Parties.Constants;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Indexing.SQL;
using OrchardCore.ContentManagement;
using YesSql;

namespace Crest.Parties.Services;

/// <summary>
/// The party ↔ user account link, from the party side: a Person may carry the
/// Orchard user that IS that person (a member's portal account, an employee's staff
/// account) in its PortalUser picker. The user side of the same link is owned by
/// whichever module creates the account (Crest.Members stamps the person id on
/// the member).
/// </summary>
public interface IPartyUserLinkService
{
    /// <summary>False when <paramref name="personId"/> is not a Person.</summary>
    Task<bool> LinkPortalUserAsync(string personId, string userId, CancellationToken cancellationToken = default);

    Task<bool> UnlinkPortalUserAsync(string personId, CancellationToken cancellationToken = default);

    Task<string?> GetPortalUserIdAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>The Person whose PortalUser is <paramref name="userId"/>, via the
    /// user-picker index; null when no person claims that account.</summary>
    Task<string?> FindPersonIdForUserAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class PartyUserLinkService(IContentManager contentManager, ISession session) : IPartyUserLinkService
{
    public const string PortalUserField = "PortalUser";

    public Task<bool> LinkPortalUserAsync(string personId, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        return SetAsync(personId, userId);
    }

    public Task<bool> UnlinkPortalUserAsync(string personId, CancellationToken cancellationToken = default)
        => SetAsync(personId, null);

    public async Task<string?> GetPortalUserIdAsync(string personId, CancellationToken cancellationToken = default)
    {
        var person = await LoadPersonAsync(personId);
        return person?.Get<ContentPart>(PartiesConstants.ContentTypes.Person)?.Get<UserPickerField>(PortalUserField)?.UserIds.FirstOrDefault();
    }

    public async Task<string?> FindPersonIdForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var index = await session.QueryIndex<UserPickerFieldIndex>(index =>
                index.ContentType == PartiesConstants.ContentTypes.Person
                && index.ContentField == PortalUserField
                && index.SelectedUserId == userId
                && index.Latest)
            .FirstOrDefaultAsync(cancellationToken);

        return index?.ContentItemId;
    }

    /// <summary>Sets the picker on an already-loaded person without saving it - the
    /// migration's path, where the caller batches the save.</summary>
    internal static void SetPortalUser(ContentItem person, string? userId) =>
        person.Alter<ContentPart>(PartiesConstants.ContentTypes.Person, part =>
            part.Alter<UserPickerField>(PortalUserField, field => field.UserIds = userId is null ? [] : [userId]));

    private async Task<bool> SetAsync(string personId, string? userId)
    {
        var person = await LoadPersonAsync(personId);
        if (person is null)
        {
            return false;
        }

        SetPortalUser(person, userId);
        await contentManager.UpdateAsync(person);
        return true;
    }

    private async Task<ContentItem?> LoadPersonAsync(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return null;
        }

        var item = await contentManager.GetAsync(personId, VersionOptions.Latest);
        return item is { ContentType: PartiesConstants.ContentTypes.Person } ? item : null;
    }
}
