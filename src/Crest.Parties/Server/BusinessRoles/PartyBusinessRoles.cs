using Crest.Access;
using Crest.ContentFields.Indexing.SQL;
using Crest.ContentManagement;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Records;
using Crest.Modules;
using Crest.Parties.Constants;
using Crest.Parties.Definitions;
using Crest.ContentManagement.Metadata;
using Crest.Parties.Indexes;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Crest.Roles;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;

namespace Crest.Parties.BusinessRoles;

/// <summary>
/// Party relationships as business roles (docs/sharing.md § 5, requirements 39-43, 48): every
/// party role a module composes (a content type a party-type provider registers, other than
/// Person and Organization) is a derived role of the same name. Where it comes from follows
/// the role's base (ruling 2026-10-14): an organization role (Customer, Vendor, Lead, ...) is
/// held by a user acting for an organization that holds it (only while acting for it,
/// requirement 42); a person role (Employee, Sales rep, ...) is held by the user whose person
/// holds it, whatever they act for. Nothing is
/// stored on the user: the role items are the only source of truth. The party-role index is the
/// content picker field index over the role items' <c>Party</c> field.
/// </summary>
/// <remarks>The account link service is resolved when asked, not when constructed: the role
/// service and the caller factory take every provider, and the content manager behind the link
/// service must not be built (or depend back on them) to list role names.</remarks>
public sealed class PartyDerivedRoleProvider(
    PartyTypeCatalog catalog,
    IServiceProvider services,
    ISession session) : IDerivedRoleProvider
{
    public const string PartyField = "Party";

    public string Name => "parties";

    public IEnumerable<DerivedRoleDefinition> Roles => RoleTypes(catalog)
        .Select(type => new DerivedRoleDefinition(type.ContentType!, $"Held by the people of a {type.DisplayName} party (a business relationship): computed, never assigned."));

    /// <summary>The party role types: tenant kinds other than the two base parties.</summary>
    public static IEnumerable<PartyTypeDescriptor> RoleTypes(PartyTypeCatalog catalog) =>
        catalog.Kinds.Where(type =>
            type.Kind == PartyTypeKinds.Tenant &&
            !string.IsNullOrEmpty(type.ContentType) &&
            type.ContentType is not PartiesConstants.ContentTypes.Person and not PartiesConstants.ContentTypes.Organization);

    public async Task<IReadOnlyList<DerivedRoleGrant>> GetRolesAsync(DerivedRoleContext context, CancellationToken cancellationToken = default)
    {
        var roleTypes = RoleTypes(catalog).Select(type => type.ContentType!).Distinct(StringComparer.Ordinal).ToArray();
        if (roleTypes.Length == 0)
        {
            return [];
        }

        var links = services.GetRequiredService<IPartyUserLinkService>();
        var personId = await links.FindPersonIdForUserAsync(context.UserId, cancellationToken);

        // The parties whose roles count: the person themselves, and the organization acted for
        // (or, for the user editor, every organization the person holds a position in).
        var organizations = new HashSet<string>(StringComparer.Ordinal);
        if (context.OrganizationId is not null)
        {
            organizations.Add(context.OrganizationId);
        }

        if (context.AllOrganizations && personId is not null)
        {
            var positions = await session.QueryIndex<PartyPositionIndex>(index => index.ContentItemId == personId && index.Published).ListAsync(cancellationToken);
            organizations.UnionWith(positions.Select(position => position.OrganizationId).Where(id => !string.IsNullOrEmpty(id)));
        }

        var parties = organizations.ToList();
        if (personId is not null)
        {
            parties.Add(personId);
        }

        if (parties.Count == 0)
        {
            return [];
        }

        var partyIds = parties.ToArray();
        var rows = await session.QueryIndex<ContentPickerFieldIndex>(index =>
                index.ContentField == PartyField &&
                index.Published &&
                index.ContentType.IsIn(roleTypes) &&
                index.SelectedContentItemId.IsIn(partyIds))
            .ListAsync(cancellationToken);

        var definitions = services.GetRequiredService<IContentDefinitionManager>();
        var bases = new Dictionary<string, PartyRoleBase?>(StringComparer.Ordinal);
        foreach (var roleType in roleTypes)
        {
            bases[roleType] = await PartyRoleBases.GetAsync(definitions, roleType);
        }

        var titles = await TitlesAsync(parties, cancellationToken);
        var grants = new List<DerivedRoleGrant>();
        foreach (var row in rows)
        {
            // A role counts only through its own base: a business role through the
            // organization acted for, a person role through the person.
            var roleBase = bases.GetValueOrDefault(row.ContentType);
            var isPersonRow = row.SelectedContentItemId == personId;
            if ((roleBase == PartyRoleBase.Organization && isPersonRow) || (roleBase == PartyRoleBase.Person && !isPersonRow))
            {
                continue;
            }

            if (isPersonRow)
            {
                grants.Add(new DerivedRoleGrant(row.ContentType, $"{row.ContentType}: {titles.GetValueOrDefault(personId!) ?? "their own person"}", null, row.ContentItemId));
            }
            else
            {
                grants.Add(new DerivedRoleGrant(row.ContentType, $"{row.ContentType}, member of {titles.GetValueOrDefault(row.SelectedContentItemId) ?? row.SelectedContentItemId}", row.SelectedContentItemId, row.ContentItemId));
            }
        }

        return grants;
    }

    private async Task<Dictionary<string, string>> TitlesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var array = ids.ToArray();
        var rows = await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(array) && index.Latest).ListAsync(cancellationToken);
        return rows
            .Where(row => !string.IsNullOrEmpty(row.DisplayText))
            .GroupBy(row => row.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayText, StringComparer.Ordinal);
    }
}

/// <summary>
/// A relationship changed (requirement 43): a role item published, unpublished, changed or
/// removed, or a person's account link or positions changed. The tenant's permission version is
/// bumped, so every cached caller is rebuilt on its next request and a removed role stops
/// counting at once.
/// </summary>
public sealed class PartyRoleChangeHandler(PartyTypeCatalog catalog, IPermissionVersion version) : ContentHandlerBase
{
    public override Task PublishedAsync(PublishContentContext context) => BumpAsync(context.ContentItem);

    public override Task UnpublishedAsync(PublishContentContext context) => BumpAsync(context.ContentItem);

    public override Task UpdatedAsync(UpdateContentContext context) => BumpAsync(context.ContentItem);

    public override Task RemovedAsync(RemoveContentContext context) => BumpAsync(context.ContentItem);

    public override Task ImportedAsync(ImportContentContext context) => BumpAsync(context.ContentItem);

    private Task BumpAsync(ContentItem item) =>
        item.ContentType == PartiesConstants.ContentTypes.Person ||
        PartyDerivedRoleProvider.RoleTypes(catalog).Any(type => type.ContentType == item.ContentType)
            ? version.BumpAsync()
            : Task.CompletedTask;
}

[Feature("Crest.Parties")]
public sealed class PartyBusinessRolesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDerivedRoleProvider, PartyDerivedRoleProvider>();
        services.AddScoped<IContentHandler, PartyRoleChangeHandler>();
    }
}

/// <summary>
/// Organizations for sharing pickers and names (docs/sharing.md § 8): the published Organization
/// parties, by name. Sharing knows the organization principal only by id; Parties knows what an
/// organization is.
/// </summary>
public sealed class PartyOrganizationDirectory(ISession session) : Crest.Sharing.ISharePrincipalDirectory
{
    public async Task<IReadOnlyList<Crest.Sharing.SharePrincipalOption>> SearchAsync(string? text, int max, CancellationToken cancellationToken = default)
    {
        var search = (text ?? string.Empty).Trim();
        var query = search.Length == 0
            ? session.QueryIndex<ContentItemIndex>(index => index.ContentType == PartiesConstants.ContentTypes.Organization && index.Published)
            : session.QueryIndex<ContentItemIndex>(index => index.ContentType == PartiesConstants.ContentTypes.Organization && index.Published && index.DisplayText.Contains(search));
        var rows = await query.OrderBy(index => index.DisplayText).Take(max).ListAsync(cancellationToken);
        return [.. rows.Select(row => new Crest.Sharing.SharePrincipalOption(Crest.Sharing.SharePrincipal.Organization(row.ContentItemId), row.DisplayText ?? row.ContentItemId, "Organization: the members acting for it"))];
    }

    public async Task<IReadOnlyDictionary<Crest.Sharing.SharePrincipal, string>> NamesAsync(IReadOnlyCollection<Crest.Sharing.SharePrincipal> principals, CancellationToken cancellationToken = default)
    {
        var ids = principals
            .Where(principal => principal.Kind == Crest.Sharing.SharePrincipalKind.Organization && principal.Id is not null)
            .Select(principal => principal.Id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Crest.Sharing.SharePrincipal, string>();
        }

        var rows = await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(ids) && index.Published).ListAsync(cancellationToken);
        return rows
            .Where(row => !string.IsNullOrEmpty(row.DisplayText))
            .GroupBy(row => row.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => Crest.Sharing.SharePrincipal.Organization(group.Key), group => group.First().DisplayText);
    }
}

[Feature("Crest.Parties")]
[RequireFeatures("Crest.Sharing")]
public sealed class PartySharingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddScoped<Crest.Sharing.ISharePrincipalDirectory, PartyOrganizationDirectory>();
}
