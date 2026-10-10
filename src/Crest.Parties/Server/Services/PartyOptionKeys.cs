using Crest.Models;
using Crest.Services;

namespace Crest.Parties.Services;

/// <summary>
/// Key ↔ option-id translation for the lists party contact data references. Option
/// pickers store option content item ids while the API and code speak KEYS (the
/// identity rule), so every read and write crosses this boundary. Scoped: each list
/// is loaded at most once per request, however many contact points reference it.
/// </summary>
public sealed class PartyOptionKeys(ICrestContentPartListService lists)
{
    private readonly Dictionary<string, ListLookup> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string?> KeyForIdAsync(string listKey, string? optionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(optionId))
        {
            return null;
        }

        var lookup = await LoadAsync(listKey, cancellationToken);
        return lookup.ById.TryGetValue(optionId, out var option) ? option.Key : null;
    }

    /// <summary>Null key → null id; an unknown key is a caller error, not "no value".</summary>
    public async Task<string?> IdForKeyAsync(string listKey, string? optionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(optionKey))
        {
            return null;
        }

        return (await RequireOptionAsync(listKey, optionKey, cancellationToken)).ContentItemId;
    }

    public async Task<CrestOptionModel> RequireOptionAsync(string listKey, string optionKey, CancellationToken cancellationToken = default)
    {
        var lookup = await LoadAsync(listKey, cancellationToken);
        if (!lookup.ByKey.TryGetValue(optionKey, out var option))
        {
            throw new InvalidOperationException($"'{optionKey}' is not an option of the '{listKey}' list.");
        }

        return option;
    }

    private async Task<ListLookup> LoadAsync(string listKey, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(listKey, out var cached))
        {
            return cached;
        }

        var list = await lists.GetAsync(listKey, cancellationToken)
            ?? throw new InvalidOperationException($"The '{listKey}' list does not exist on this tenant.");

        var lookup = new ListLookup(
            list.Options.ToDictionary(option => option.ContentItemId, StringComparer.OrdinalIgnoreCase),
            list.Options.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase));
        _cache[listKey] = lookup;
        return lookup;
    }

    private sealed record ListLookup(
        Dictionary<string, CrestOptionModel> ById,
        Dictionary<string, CrestOptionModel> ByKey);
}
