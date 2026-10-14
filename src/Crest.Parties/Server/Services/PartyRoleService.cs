using Crest.Access;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Indexing.SQL;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.Parties.Constants;
using Crest.Parties.Definitions;
using Crest.Parties.ViewModels;
using YesSql;

namespace Crest.Parties.Services;


/// <summary>
/// Gives a party a role (ruling 2026-10-14). The role declares its base: an organization
/// role added to a person lands on the person's Individual organization (created when
/// missing, "make this person a customer"); a person role added to an Individual
/// organization lands on its person; any other mismatch is refused. A party that already
/// holds the role keeps it. Authorized for the caller: viewing the party, publishing the role.
/// </summary>
public interface IPartyRoleService
{
    Task<PartyRoleResult> AddRoleAsync(string roleContentType, string partyId, CallerContext? caller, CancellationToken cancellationToken = default);
}

public sealed class PartyRoleService(
    IContentManager contentManager,
    IContentDefinitionManager definitions,
    IIndividualOrganizationService individuals,
    IAccessDecision decision,
    ISession session) : IPartyRoleService
{
    public async Task<PartyRoleResult> AddRoleAsync(string roleContentType, string partyId, CallerContext? caller, CancellationToken cancellationToken = default)
    {
        var roleBase = await PartyRoleBases.GetAsync(definitions, roleContentType)
            ?? throw new InvalidOperationException($"'{roleContentType}' is not a party role type.");

        var party = string.IsNullOrWhiteSpace(partyId) ? null : await contentManager.GetAsync(partyId, VersionOptions.Latest);
        if (party is null || party.ContentType is not (PartiesConstants.ContentTypes.Person or PartiesConstants.ContentTypes.Organization))
        {
            throw new InvalidOperationException($"'{partyId}' is not a Person or Organization.");
        }

        await decision.RequireAsync(caller, CommonPermissionNames.ViewContent, party, cancellationToken);

        var individual = false;
        var target = party;
        if (roleBase == PartyRoleBase.Organization && party.ContentType == PartiesConstants.ContentTypes.Person)
        {
            if (await individuals.FindForPersonAsync(party.ContentItemId, cancellationToken) is not { } existingOrganization)
            {
                // Creating the Individual organization is a write of its own.
                var draft = await contentManager.NewAsync(PartiesConstants.ContentTypes.Organization);
                await decision.RequireAsync(caller, CommonPermissionNames.PublishContent, draft, cancellationToken);
                existingOrganization = await individuals.EnsureForPersonAsync(party, cancellationToken);
            }

            target = existingOrganization;
            individual = true;
        }
        else if (roleBase == PartyRoleBase.Person && party.ContentType == PartiesConstants.ContentTypes.Organization)
        {
            var personId = await individuals.PersonOfAsync(party.ContentItemId, cancellationToken)
                ?? throw new InvalidOperationException($"'{roleContentType}' is a person role; give it to a person, not an organization.");
            target = await contentManager.GetAsync(personId, VersionOptions.Latest)
                ?? throw new InvalidOperationException("The Individual organization's person no longer exists.");
        }

        var existing = await session.QueryIndex<ContentPickerFieldIndex>(index =>
                index.ContentType == roleContentType &&
                index.ContentField == PartyRoleDefinitions.PartyField &&
                index.SelectedContentItemId == target.ContentItemId &&
                index.Latest)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new PartyRoleResult(existing.ContentItemId, roleContentType, target.ContentItemId, false, individual);
        }

        var role = await contentManager.NewAsync(roleContentType);
        await decision.RequireAsync(caller, CommonPermissionNames.PublishContent, role, cancellationToken);
        role.DisplayText = target.DisplayText;
        ((System.Text.Json.Nodes.JsonObject)role.Content)["TitlePart"] = new System.Text.Json.Nodes.JsonObject { ["Title"] = target.DisplayText };
        role.Alter<ContentPart>(roleContentType, part => part.Alter<ContentPickerField>(PartyRoleDefinitions.PartyField, field => field.ContentItemIds = [target.ContentItemId]));
        await contentManager.CreateAsync(role, VersionOptions.Published);

        return new PartyRoleResult(role.ContentItemId, roleContentType, target.ContentItemId, true, individual);
    }
}

internal static class CommonPermissionNames
{
    public const string ViewContent = "ViewContent";
    public const string PublishContent = "PublishContent";
}
