using Grove.Access;
using Grove.ContentManagement;
using Grove.Parties.Constants;
using Grove.Parties.Indexes;
using YesSql;

namespace Grove.Parties.Services;

/// <summary>One association as the reading API returns it; type and ends by id, the type also as its option key.</summary>
public sealed record AssociationModel(
    string Id,
    string? Type,
    string? FromPersonId,
    string? FromOrganizationId,
    string? ToPersonId,
    string? ToOrganizationId);

/// <summary>
/// What an association type grants. An association grants no visibility by default; a
/// module (or, later, a tenant) registers a resolver for a type to decide per type
/// whether the far end (the organization's people) may see the association, and which
/// parties' records its holder may see (ruling 2026-10-05).
/// </summary>
public interface IAssociationVisibilityResolver
{
    /// <summary>The association-type option key this resolver decides for.</summary>
    string TypeKey { get; }

    /// <summary>True when the far end of an association of this type may see it. Default: no.</summary>
    bool CounterpartMaySee { get; }

    /// <summary>The parties whose records the association's holder (its from end) may see through it. Default: none.</summary>
    Task<IReadOnlyList<string>> GrantedPartyIdsAsync(AssociationModel association, CancellationToken cancellationToken = default);
}

/// <summary>
/// The reading side of associations (writes are ordinary content item writes, so editing,
/// versioning, permissions and audit come from the content system). A person sees the
/// associations they hold (the from end); the far end sees one only when its type's
/// resolver allows it, so the organization's people do not see the rep side by default.
/// </summary>
public interface IPartyAssociationService
{
    /// <summary>The associations visible to <paramref name="personId"/>: every one they hold,
    /// plus those pointing at them whose type lets the counterpart see.</summary>
    Task<IReadOnlyList<AssociationModel>> ListForPersonAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>The parties whose records <paramref name="personId"/> may see through their
    /// associations, as their types' resolvers grant (none by default).</summary>
    Task<IReadOnlyList<string>> GrantedPartyIdsAsync(string personId, CancellationToken cancellationToken = default);
}

public sealed class PartyAssociationService(
    ISession session,
    PartyOptionKeys optionKeys,
    IEnumerable<IAssociationVisibilityResolver> resolvers) : IPartyAssociationService
{
    public async Task<IReadOnlyList<AssociationModel>> ListForPersonAsync(string personId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return [];
        }

        var held = await session.QueryIndex<AssociationIndex>(index => index.FromPersonId == personId && index.Published).ListAsync(cancellationToken);
        var pointing = await session.QueryIndex<AssociationIndex>(index => index.ToPersonId == personId && index.Published).ListAsync(cancellationToken);

        var models = new List<AssociationModel>();
        foreach (var row in held)
        {
            models.Add(await ToModelAsync(row, cancellationToken));
        }

        foreach (var row in pointing)
        {
            var model = await ToModelAsync(row, cancellationToken);
            if (Resolver(model.Type) is { CounterpartMaySee: true } && models.All(existing => existing.Id != model.Id))
            {
                models.Add(model);
            }
        }

        return models;
    }

    public async Task<IReadOnlyList<string>> GrantedPartyIdsAsync(string personId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            return [];
        }

        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in await session.QueryIndex<AssociationIndex>(index => index.FromPersonId == personId && index.Published).ListAsync(cancellationToken))
        {
            var model = await ToModelAsync(row, cancellationToken);
            if (Resolver(model.Type) is { } resolver)
            {
                granted.UnionWith(await resolver.GrantedPartyIdsAsync(model, cancellationToken));
            }
        }

        return [.. granted];
    }

    private IAssociationVisibilityResolver? Resolver(string? typeKey) =>
        typeKey is null ? null : resolvers.FirstOrDefault(resolver => string.Equals(resolver.TypeKey, typeKey, StringComparison.OrdinalIgnoreCase));

    private async Task<AssociationModel> ToModelAsync(AssociationIndex row, CancellationToken cancellationToken) => new(
        row.ContentItemId,
        await optionKeys.KeyForIdAsync(PartiesOptionSets.AssociationType, row.TypeId, cancellationToken),
        row.FromPersonId,
        row.FromOrganizationId,
        row.ToPersonId,
        row.ToOrganizationId);
}
