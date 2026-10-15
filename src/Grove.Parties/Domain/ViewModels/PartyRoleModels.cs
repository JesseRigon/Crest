using System.Text.Json.Nodes;
using Grove.Parties.Constants;
using Grove.Parties.PartyTypes;

namespace Grove.Parties.ViewModels;

/// <summary>Gives a party a role of a registered party type (<c>POST api/grove/parties/{id}/roles</c>).</summary>
public sealed record AddPartyRoleRequest(string TypeKey);

/// <summary>The outcome of giving a party a role: the role item, the party it points at (an
/// Individual organization when an organization role was added to a person), and whether it
/// was created now.</summary>
public sealed record PartyRoleResult(string RoleId, string RoleContentType, string PartyId, bool Created, bool IndividualOrganization);

/// <summary>The Parties tenant settings as the API carries them.</summary>
public sealed record PartiesSettingsModel(bool AllowIndividualConversion);

/// <summary>
/// Reading a party's content item JSON the way every Parties screen does (ruling 2026-10-14):
/// an Individual organization reads as its person, so lists mark it "Individual" rather than
/// presenting it as a company. Server-free, shared by the server and the Blazor panes.
/// </summary>
public static class PartyDisplay
{
    /// <summary>True when the item content is an Organization flagged Individual.</summary>
    public static bool IsIndividual(string? contentType, JsonObject? content) =>
        contentType == PartiesConstants.ContentTypes.Organization
        && content?[PartiesConstants.ContentTypes.Organization]?[PartiesConstants.OrganizationFields.Individual]?["Value"] is JsonValue value
        && value.TryGetValue<bool>(out var individual)
        && individual;

    /// <summary>The account a Person is linked to, read from its PortalUser picker; null when none.</summary>
    public static string? PortalUserId(JsonObject? content) =>
        content?[PartiesConstants.ContentTypes.Person]?["PortalUser"]?["UserIds"] is JsonArray ids
            ? ids.Select(id => id?.ToString()).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))
            : null;

    /// <summary>The kind label a list shows for a party: "Individual" for an Individual
    /// organization, else the content type.</summary>
    public static string KindLabel(string? contentType, JsonObject? content) =>
        IsIndividual(contentType, content) ? "Individual" : contentType ?? "Party";

    /// <summary>The party types a role can be added from: tenant kinds composed on a base party,
    /// not the two base parties themselves.</summary>
    public static IReadOnlyList<PartyTypeModel> RoleTypes(IEnumerable<PartyTypeModel> types) =>
        [.. types.Where(type =>
            type.Kind == PartyTypeKinds.Tenant
            && !string.IsNullOrEmpty(type.ContentType)
            && type.ContentType is not PartiesConstants.ContentTypes.Person and not PartiesConstants.ContentTypes.Organization)];
}
