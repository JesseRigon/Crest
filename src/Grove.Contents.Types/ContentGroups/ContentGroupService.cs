#nullable enable

using Grove.Documents;
using Grove.Entities;
using Grove.Infrastructure.Settings;

namespace Grove.ContentGroups;

/// <summary>
/// Effective content groups = provider declarations merged with the tenant document,
/// entries resolved through the registered kind resolvers. Scoped; resolution is
/// cached for the request.
/// </summary>
public sealed class ContentGroupService(
    IEnumerable<IContentGroupProvider> providers,
    IEnumerable<IContentGroupEntryResolver> resolvers,
    IDocumentManager<ContentGroupsDocument> documents,
    ISiteService siteService)
{
    private readonly Dictionary<string, ContentGroupResolvedEntry?> _resolved = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ContentGroupModel>> ListAsync(CancellationToken cancellationToken = default) =>
        await ListAsync(await documents.GetOrCreateImmutableAsync(), cancellationToken);

    // The shared cache only refreshes after the request commits, so a write returns
    // its result from the mutable document it just changed, never from a re-read.
    private async Task<IReadOnlyList<ContentGroupModel>> ListAsync(ContentGroupsDocument document, CancellationToken cancellationToken)
    {
        var declared = Declared();
        var models = new List<ContentGroupModel>();

        foreach (var (key, definition, @override) in Merge(declared, document))
        {
            var entries = new List<ContentGroupEntryModel>();

            foreach (var entry in definition?.Entries ?? [])
            {
                if (@override?.Removed.Any(removed => removed.Matches(entry.Kind, entry.Key)) == true)
                {
                    continue;
                }

                entries.Add(await ToEntryModelAsync(entry, ContentGroupSources.Module, cancellationToken));
            }

            foreach (var entry in @override?.Added ?? [])
            {
                if (!entries.Any(existing => string.Equals(existing.Kind, entry.Kind, StringComparison.OrdinalIgnoreCase) && string.Equals(existing.Key, entry.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    entries.Add(await ToEntryModelAsync(entry, ContentGroupSources.Tenant, cancellationToken));
                }
            }

            models.Add(new ContentGroupModel(
                key,
                @override?.DisplayName ?? definition?.DisplayName ?? key,
                @override?.Position ?? definition?.Position ?? 0,
                @override?.Hidden ?? false,
                definition is null ? ContentGroupSources.Tenant : ContentGroupSources.Module,
                entries));
        }

        return models
            .OrderBy(group => group.Position)
            .ThenBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Null when the group does not exist; an existing group with nothing
    /// resolvable yields an empty filter (the caller shows no items, not all).</summary>
    public async Task<ContentGroupFilter?> ResolveFilterAsync(string groupKey, CancellationToken cancellationToken = default)
    {
        var group = (await ListAsync(cancellationToken)).FirstOrDefault(candidate => string.Equals(candidate.Key, groupKey, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            return null;
        }

        var types = new List<string>();
        var ids = new List<string>();
        foreach (var entry in group.Entries)
        {
            var resolved = await ResolveAsync(new ContentGroupEntryRef(entry.Kind, entry.Key), cancellationToken);
            if (resolved is null)
            {
                continue;
            }

            types.AddRange(resolved.ContentTypes);
            ids.AddRange(resolved.ContentItemIds);
        }

        return new ContentGroupFilter(
            types.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<ContentGroupModel> UpdateGroupAsync(string key, string? displayName, int? position, bool? hidden, CancellationToken cancellationToken = default)
    {
        key = RequireKey(key);
        var document = await documents.GetOrCreateMutableAsync();
        var @override = GetOrCreateOverride(document, key);

        if (displayName is not null)
        {
            @override.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        }

        if (position is not null)
        {
            @override.Position = position;
        }

        if (hidden is not null)
        {
            @override.Hidden = hidden.Value;
        }

        await documents.UpdateAsync(document);
        return await RequireGroupAsync(key, document, cancellationToken);
    }

    public async Task<ContentGroupModel> AddEntryAsync(string key, string kind, string entryKey, CancellationToken cancellationToken = default)
    {
        key = RequireKey(key);
        var entry = new ContentGroupEntryRef(kind.Trim().ToLowerInvariant(), entryKey.Trim());
        if (!resolvers.Any(resolver => string.Equals(resolver.Kind, entry.Kind, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"'{entry.Kind}' is not a known content group entry kind.");
        }

        if (await ResolveAsync(entry, cancellationToken) is null)
        {
            throw new InvalidOperationException($"'{entry.Key}' is not a {entry.Kind} on this tenant.");
        }

        var document = await documents.GetOrCreateMutableAsync();
        var @override = GetOrCreateOverride(document, key);
        @override.Removed.RemoveAll(removed => removed.Matches(entry.Kind, entry.Key));

        var declared = Declared().FirstOrDefault(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));
        if (declared?.Entries.Any(existing => existing.Matches(entry.Kind, entry.Key)) != true
            && !@override.Added.Any(existing => existing.Matches(entry.Kind, entry.Key)))
        {
            @override.Added.Add(entry);
        }

        await documents.UpdateAsync(document);
        return await RequireGroupAsync(key, document, cancellationToken);
    }

    public async Task<ContentGroupModel?> RemoveEntryAsync(string key, string kind, string entryKey, CancellationToken cancellationToken = default)
    {
        key = RequireKey(key);
        var group = (await ListAsync(cancellationToken)).FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            return null;
        }

        var document = await documents.GetOrCreateMutableAsync();
        var @override = GetOrCreateOverride(document, key);
        var removedFromAdded = @override.Added.RemoveAll(added => added.Matches(kind, entryKey)) > 0;

        var declared = Declared().FirstOrDefault(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));
        if (!removedFromAdded && declared?.Entries.Any(existing => existing.Matches(kind, entryKey)) == true
            && !@override.Removed.Any(removed => removed.Matches(kind, entryKey)))
        {
            @override.Removed.Add(new ContentGroupEntryRef(kind.Trim().ToLowerInvariant(), entryKey.Trim()));
        }

        await documents.UpdateAsync(document);
        return await RequireGroupAsync(key, document, cancellationToken);
    }

    /// <summary>A tenant-owned group is deleted outright; a module-declared group can
    /// only be hidden (its key is the module's contract), so this hides it.</summary>
    public async Task<bool> DeleteGroupAsync(string key, CancellationToken cancellationToken = default)
    {
        key = RequireKey(key);
        var document = await documents.GetOrCreateMutableAsync();
        var declared = Declared().Any(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));

        if (declared)
        {
            GetOrCreateOverride(document, key).Hidden = true;
        }
        else if (!document.Groups.Remove(key))
        {
            return false;
        }

        await documents.UpdateAsync(document);
        return true;
    }

    public Task<GroveContentGroupsSettings> GetSettingsAsync() => siteService.GetSettingsAsync<GroveContentGroupsSettings>();

    public async Task<GroveContentGroupsSettings> UpdateSettingsAsync(bool autoMenuPages)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<GroveContentGroupsSettings>(settings => settings.AutoMenuPages = autoMenuPages);
        await siteService.UpdateSiteSettingsAsync(site);
        return new GroveContentGroupsSettings { AutoMenuPages = autoMenuPages };
    }

    private IReadOnlyList<ContentGroupDefinition> Declared()
    {
        var builder = new ContentGroupBuilder();
        foreach (var provider in providers)
        {
            provider.Build(builder);
        }

        return builder.Build();
    }

    private static IEnumerable<(string Key, ContentGroupDefinition? Definition, ContentGroupOverride? Override)> Merge(
        IReadOnlyList<ContentGroupDefinition> declared,
        ContentGroupsDocument document)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in declared)
        {
            seen.Add(definition.Key);
            document.Groups.TryGetValue(definition.Key, out var @override);
            yield return (definition.Key, definition, @override);
        }

        foreach (var (key, @override) in document.Groups)
        {
            if (seen.Add(key))
            {
                yield return (key, null, @override);
            }
        }
    }

    private async Task<ContentGroupEntryModel> ToEntryModelAsync(ContentGroupEntryRef entry, string source, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(entry, cancellationToken);
        return new ContentGroupEntryModel(entry.Kind, entry.Key, resolved?.DisplayName ?? entry.Key, source, resolved is not null);
    }

    private async Task<ContentGroupResolvedEntry?> ResolveAsync(ContentGroupEntryRef entry, CancellationToken cancellationToken)
    {
        var cacheKey = $"{entry.Kind}:{entry.Key}";
        if (_resolved.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var resolver = resolvers.FirstOrDefault(candidate => string.Equals(candidate.Kind, entry.Kind, StringComparison.OrdinalIgnoreCase));
        var resolved = resolver is null ? null : await resolver.ResolveAsync(entry.Key, cancellationToken);
        _resolved[cacheKey] = resolved;
        return resolved;
    }

    private async Task<ContentGroupModel> RequireGroupAsync(string key, ContentGroupsDocument document, CancellationToken cancellationToken) =>
        (await ListAsync(document, cancellationToken)).First(group => string.Equals(group.Key, key, StringComparison.OrdinalIgnoreCase));

    private static ContentGroupOverride GetOrCreateOverride(ContentGroupsDocument document, string key)
    {
        if (!document.Groups.TryGetValue(key, out var @override))
        {
            @override = new ContentGroupOverride();
            document.Groups[key] = @override;
        }

        return @override;
    }

    private string RequireKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("A content group needs a key.");
        }

        key = key.Trim();
        var declared = Declared().FirstOrDefault(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));
        return declared?.Key ?? key;
    }

    /// <summary>Marks a group the tenant creates as tenant-owned so deletion removes it.</summary>
    public async Task<ContentGroupModel> CreateGroupAsync(string key, string displayName, int position, CancellationToken cancellationToken = default)
    {
        key = RequireKey(key);
        if (Declared().Any(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"'{key}' is declared by a module; rename or reorder it instead of creating it.");
        }

        var document = await documents.GetOrCreateMutableAsync();
        if (document.Groups.ContainsKey(key))
        {
            throw new InvalidOperationException($"A content group '{key}' already exists.");
        }

        document.Groups[key] = new ContentGroupOverride
        {
            TenantOwned = true,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim(),
            Position = position,
        };
        await documents.UpdateAsync(document);
        return await RequireGroupAsync(key, document, cancellationToken);
    }
}
