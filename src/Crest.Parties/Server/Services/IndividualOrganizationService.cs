using Crest.Access;
using Crest.ContentFields.Fields;
using Crest.ContentManagement;
using Crest.Parties.Constants;
using Crest.Parties.Indexes;
using Crest.Parties.ViewModels;
using Crest.Settings;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace Crest.Parties.Services;

/// <summary>The Parties tenant settings.</summary>
public sealed class PartiesSettings
{
    /// <summary>Whether an Individual organization may be converted into an ordinary one (it
    /// then takes more members). Beside self-registration; default: not allowed.</summary>
    public bool AllowIndividualConversion { get; set; }
}

/// <summary>
/// Individual organizations (ruling 2026-10-14): an individual in a business relationship
/// (a coffee shop's regular, a street vendor) is an Organization of kind Individual, named
/// after the person, created when an organization role is added to a person. The person
/// holds a position in it and is its owner; it has exactly one member and reads as the
/// person. A household or a job site is an ordinary organization, not this.
/// </summary>
public interface IIndividualOrganizationService
{
    /// <summary>The person's Individual organization, created (with their position and,
    /// when they have an account, their ownership) when they have none.</summary>
    Task<ContentItem> EnsureForPersonAsync(ContentItem person, CancellationToken cancellationToken = default);

    Task<ContentItem?> FindForPersonAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>The one person of an Individual organization; null for an ordinary one.</summary>
    Task<string?> PersonOfAsync(string organizationId, CancellationToken cancellationToken = default);

    /// <summary>Makes an Individual organization ordinary. Refused unless the tenant allows it.</summary>
    Task ConvertToOrdinaryAsync(ContentItem organization, CancellationToken cancellationToken = default);
}

public sealed class IndividualOrganizationService(
    IContentManager contentManager,
    IPartyPositionsService positions,
    IPartyUserLinkService userLink,
    ISiteService siteService,
    ISession session,
    IServiceProvider services) : IIndividualOrganizationService
{
    public static bool IsIndividual(ContentItem? organization) =>
        organization is { ContentType: PartiesConstants.ContentTypes.Organization }
        && organization.Get<ContentPart>(PartiesConstants.ContentTypes.Organization)?.Get<BooleanField>(PartiesConstants.OrganizationFields.Individual)?.Value == true;

    public async Task<ContentItem> EnsureForPersonAsync(ContentItem person, CancellationToken cancellationToken = default)
    {
        if (person.ContentType != PartiesConstants.ContentTypes.Person)
        {
            throw new InvalidOperationException("An Individual organization is created for a Person.");
        }

        if (await FindForPersonAsync(person.ContentItemId, cancellationToken) is { } existing)
        {
            return existing;
        }

        var organization = await contentManager.NewAsync(PartiesConstants.ContentTypes.Organization);
        organization.DisplayText = string.IsNullOrWhiteSpace(person.DisplayText) ? "Individual" : person.DisplayText;
        ((System.Text.Json.Nodes.JsonObject)organization.Content)["TitlePart"] = new System.Text.Json.Nodes.JsonObject { ["Title"] = organization.DisplayText };
        organization.Alter<ContentPart>(PartiesConstants.ContentTypes.Organization, part =>
            part.Alter<BooleanField>(PartiesConstants.OrganizationFields.Individual, field => field.Value = true));
        await contentManager.CreateAsync(organization, VersionOptions.Published);

        await positions.AddAsync(person, new PositionWriteModel(organization.ContentItemId), cancellationToken);

        // The person is the owner. Ownership is recorded per user account
        // (IOrganizationOwnership, Members); a person without an account becomes the owner
        // when they are promoted to the portal (their binding is the organization's first).
        if (await userLink.GetPortalUserIdAsync(person.ContentItemId, cancellationToken) is { } userId
            && services.GetService<IOrganizationOwnership>() is { } ownership)
        {
            await ownership.EnsureOwnerAsync(organization.ContentItemId, userId, cancellationToken);
        }

        return organization;
    }

    public async Task<ContentItem?> FindForPersonAsync(string personId, CancellationToken cancellationToken = default)
    {
        var rows = await session.QueryIndex<PartyPositionIndex>(index => index.ContentItemId == personId && index.Latest).ListAsync(cancellationToken);
        var organizationIds = rows.Select(row => row.OrganizationId).Distinct(StringComparer.Ordinal).ToArray();
        if (organizationIds.Length == 0)
        {
            return null;
        }

        return (await contentManager.GetAsync(organizationIds, VersionOptions.Latest)).FirstOrDefault(IsIndividual);
    }

    public async Task<string?> PersonOfAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        if (!IsIndividual(await contentManager.GetAsync(organizationId, VersionOptions.Latest)))
        {
            return null;
        }

        return (await session.QueryIndex<PartyPositionIndex>(index => index.OrganizationId == organizationId && index.Latest).FirstOrDefaultAsync(cancellationToken))?.ContentItemId;
    }

    public async Task ConvertToOrdinaryAsync(ContentItem organization, CancellationToken cancellationToken = default)
    {
        if (!IsIndividual(organization))
        {
            throw new InvalidOperationException("The organization is not an Individual organization.");
        }

        if (!(await siteService.GetSettingsAsync<PartiesSettings>()).AllowIndividualConversion)
        {
            throw new InvalidOperationException("This tenant does not allow converting an Individual organization into an ordinary one.");
        }

        organization.Alter<ContentPart>(PartiesConstants.ContentTypes.Organization, part =>
            part.Alter<BooleanField>(PartiesConstants.OrganizationFields.Individual, field => field.Value = false));
        await contentManager.UpdateAsync(organization);
        if (!organization.Published)
        {
            await contentManager.PublishAsync(organization);
        }
    }
}
