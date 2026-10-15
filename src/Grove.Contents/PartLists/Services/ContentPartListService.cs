using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Grove.Fields;
using Grove.Migrations;
using Grove.Models;
using Grove.Infrastructure.Settings;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Management.Records;
using Grove.Data.Global.Lists;
using YesSql;

namespace Grove.Services;

// Two kinds of list behind one contract (docs/global-store.md):
//  - TENANT lists are content items whose GroveContentPartListPart holds the Option items
//    directly; tenants edit them like any other content.
//  - GLOBAL-BACKED lists have their standard rows in the tenant-less global store. The
//    tenant's content item for such a key - created lazily on the first tenant write -
//    holds only the tenant's ADDITIONS plus an Overrides map (label, plural, hidden,
//    position) over the standard rows. Standard rows get deterministic ids
//    (GlobalLists.OptionId) so pickers can store them without a tenant content item.
// Consumers see one merged GroveContentPartListModel either way.
public sealed class ContentPartListService(
    ISession session,
    IContentManager contentManager,
    IContentDefinitionManager contentDefinitionManager,
    IGlobalListReader globalLists) : IContentPartListService
{
    // Per-request memo only. Sets are read repeatedly while resolving a document's
    // lines, but they are content items a tenant may edit at any time, so caching
    // beyond the request would need invalidation on save; scoped memoization gets the
    // hot-path win without that risk.
    private readonly Dictionary<string, GroveContentPartListModel?> _byKey = new(ContentPartListRules.KeyComparer);

    // Custom-field descriptors per option content type, memoized alongside _byKey for
    // the same reason: the type definition is read for every model projection.
    private readonly Dictionary<string, GroveOptionFieldModel[]> _fieldsByType = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<GroveContentPartListModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == GroveContentPartListMigrations.ContentPartListContentType && index.Latest)
            .ListAsync();
        var itemsByKey = items
            .GroupBy(item => ContentPartListRules.NormalizeKey(item.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name)?.Key), ContentPartListRules.KeyComparer)
            .ToDictionary(group => group.Key, group => group.First(), ContentPartListRules.KeyComparer);

        var models = new List<GroveContentPartListModel>();
        foreach (var global in await globalLists.ListAsync(cancellationToken))
        {
            var tenantItem = itemsByKey.GetValueOrDefault(global.Key);
            itemsByKey.Remove(global.Key);
            models.Add(Merge(global, tenantItem, tenantItem is null ? [] : await GetOptionFieldsAsync(tenantItem)));
        }

        foreach (var item in itemsByKey.Values)
        {
            models.Add(ToModel(item, await GetOptionFieldsAsync(item)));
        }

        return [.. models.OrderBy(list => list.DisplayText, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<GroveContentPartListModel?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = ContentPartListRules.NormalizeKey(key);
        if (normalized.Length == 0)
        {
            return null;
        }

        if (_byKey.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        var item = await FindListItemAsync(normalized);
        var global = await globalLists.GetAsync(normalized, cancellationToken);
        GroveContentPartListModel? model;
        if (global is not null)
        {
            model = Merge(global, item, item is null ? [] : await GetOptionFieldsAsync(item));
        }
        else
        {
            model = item is null ? null : ToModel(item, await GetOptionFieldsAsync(item));
        }

        _byKey[normalized] = model;
        return model;
    }

    public async Task<IReadOnlyList<GroveOptionModel>> GetSelectableAsync(string key, CancellationToken cancellationToken = default)
    {
        var list = await GetAsync(key, cancellationToken);
        return list is null ? [] : ContentPartListRules.Selectable(list.Options);
    }

    public async Task<string?> ResolveKeyAsync(string listKey, string optionContentItemId, CancellationToken cancellationToken = default)
    {
        var list = await GetAsync(listKey, cancellationToken);
        return list?.Options
            .FirstOrDefault(option => string.Equals(option.ContentItemId, optionContentItemId, StringComparison.OrdinalIgnoreCase))
            ?.Key;
    }

    public async Task<string?> ResolveContentItemIdAsync(string listKey, string optionKey, CancellationToken cancellationToken = default)
    {
        var list = await GetAsync(listKey, cancellationToken);
        var normalized = ContentPartListRules.NormalizeKey(optionKey);
        return list?.Options
            .FirstOrDefault(option => ContentPartListRules.KeyComparer.Equals(option.Key, normalized))
            ?.ContentItemId;
    }

    public async Task<GroveContentPartListModel> SeedAsync(ContentPartListSeed seed, CancellationToken cancellationToken = default)
    {
        var listKey = ContentPartListRules.NormalizeKey(seed.Key);
        if (listKey.Length == 0)
        {
            throw new ArgumentException("A seeded content part list needs a key.", nameof(seed));
        }

        var global = await globalLists.GetAsync(listKey, cancellationToken);
        if (global is not null)
        {
            // The standard rows are the global store's; a module seed can only layer
            // tenant-store additions over them, and only for keys the standard lacks.
            var merged = await GetAsync(listKey, cancellationToken) ?? throw new InvalidOperationException($"Global list '{listKey}' could not be read.");
            var additions = ContentPartListRules.PlanSeed(seed, merged.Options);
            if (additions.HasChanges)
            {
                var tenantItem = await FindListItemAsync(listKey) ?? await CreateListItemAsync(listKey, global.DisplayText, OptionSources.Module);
                var next = merged.Options.Length == 0 ? 0 : merged.Options.Max(option => option.Position) + 1;
                foreach (var addition in additions.Additions)
                {
                    var position = addition.Position != 0 ? addition.Position : next++;
                    await AppendOptionAsync(tenantItem, addition.Key, addition.DisplayText, position, OptionSources.Module, addition.Category, addition.DisplayTextPlural, addition.Value, addition.Parent);
                }

                await contentManager.UpdateAsync(tenantItem);
                await contentManager.PublishAsync(tenantItem);
            }

            _byKey.Remove(listKey);
            return (await GetAsync(listKey, cancellationToken))!;
        }

        var listItem = await FindListItemAsync(listKey)
            ?? await CreateListItemAsync(listKey, seed.DisplayText, OptionSources.Module);

        // Module locks are the developer's contract, re-asserted on every reseed -
        // unlike sort or labels there is no tenant edit of them to preserve (the API
        // refuses to touch a Module lock). Asserted, never cleared: dropping a lock
        // from a seed downgrades to whatever the tenant state says.
        var currentPart = listItem.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name);
        var wantsDataLock = seed.DataLock && currentPart?.DataLock != ContentPartListLockSources.Module;
        var wantsEditLock = seed.EditLock && currentPart?.EditLock != ContentPartListLockSources.Module;
        if (wantsDataLock || wantsEditLock)
        {
            listItem.Alter<GroveContentPartListPart>(part =>
            {
                part.DataLock = seed.DataLock ? ContentPartListLockSources.Module : part.DataLock;
                part.EditLock = seed.EditLock ? ContentPartListLockSources.Module : part.EditLock;
            });
            await contentManager.UpdateAsync(listItem);
            await contentManager.PublishAsync(listItem);
        }

        // Existing options are matched by key and left untouched, so a tenant's
        // relabel/hide survives every reseed.
        var existingOptions = ToModel(listItem).Options;
        var plan = ContentPartListRules.PlanSeed(seed, existingOptions);
        if (plan.HasChanges)
        {
            // Seeds that don't set explicit positions get sequential ones in seed
            // order, appended after what the list already holds - the manual order
            // (and the Position index column) starts meaningful instead of all-zero.
            var nextPosition = existingOptions.Length == 0 ? 0 : existingOptions.Max(option => option.Position) + 1;
            foreach (var addition in plan.Additions)
            {
                var position = addition.Position != 0 ? addition.Position : nextPosition++;
                await AppendOptionAsync(listItem, addition.Key, addition.DisplayText, position, OptionSources.Module, addition.Category, addition.DisplayTextPlural, addition.Value, addition.Parent);
            }

            await contentManager.UpdateAsync(listItem);
            await contentManager.PublishAsync(listItem);
        }

        _byKey.Remove(listKey);
        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
    }

    public async Task<GroveContentPartListModel> CreateListAsync(string key, string displayText, CancellationToken cancellationToken = default)
    {
        var normalized = ContentPartListRules.NormalizeKey(key);
        var existing = await ListAsync(cancellationToken);
        var validation = ContentPartListRules.ValidateKey(normalized, existing.Select(list => list.Key));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var item = await CreateListItemAsync(normalized, displayText, OptionSources.Tenant);
        _byKey.Remove(normalized);
        return ToModel(item, await GetOptionFieldsAsync(item));
    }

    public async Task<GroveOptionModel> AddOptionAsync(string listKey, string optionKey, string displayText, int position = 0, string? category = null, string? displayTextPlural = null, string? value = null, IReadOnlyDictionary<string, string?>? fields = null, string? parent = null, CancellationToken cancellationToken = default)
    {
        var merged = await GetAsync(listKey, cancellationToken)
            ?? throw new InvalidOperationException($"There is no content part list with the key '{listKey}'.");
        var listItem = await FindListItemAsync(merged.Key)
            ?? await CreateListItemAsync(merged.Key, merged.DisplayText, OptionSources.Module);
        var validation = ContentPartListRules.ValidateKey(optionKey, merged.Options.Select(option => option.Key));
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var descriptors = await GetOptionFieldsAsync(listItem);
        var normalized = ContentPartListRules.NormalizeKey(optionKey);
        if (ContentPartListRules.ValidateParent(merged.Options, normalized, parent) is { } parentError)
        {
            throw new InvalidOperationException(parentError);
        }

        var option = await AppendOptionAsync(listItem, normalized, displayText, position, OptionSources.Tenant, category, displayTextPlural, value, parent);
        if (fields is { Count: > 0 })
        {
            WriteFields(option, fields, descriptors);
        }

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(ContentPartListRules.NormalizeKey(listKey));

        return ToOptionModel(option, descriptors);
    }

    public async Task<GroveOptionModel> UpdateOptionAsync(string listKey, string optionKey, string? displayText, int? position, bool? hidden, string? category = null, string? displayTextPlural = null, string? value = null, IReadOnlyDictionary<string, string?>? fields = null, string? parent = null, CancellationToken cancellationToken = default)
    {
        var normalized = ContentPartListRules.NormalizeKey(optionKey);
        var global = await globalLists.GetAsync(ContentPartListRules.NormalizeKey(listKey), cancellationToken);
        if (global is not null && global.Options.Any(candidate => ContentPartListRules.KeyComparer.Equals(candidate.Key, normalized)))
        {
            // A standard row: the tenant may change how it LOOKS, never what it IS.
            if (category is not null || value is not null || parent is not null || fields is { Count: > 0 })
            {
                throw new InvalidOperationException($"'{optionKey}' is a standard option of the global list '{listKey}'; its category, parent, value and data fields cannot be changed by a tenant. Label, plural, hidden and position can.");
            }

            var tenantItem = await FindListItemAsync(global.Key) ?? await CreateListItemAsync(global.Key, global.DisplayText, OptionSources.Module);
            tenantItem.Alter<GroveContentPartListPart>(part =>
            {
                if (!part.Overrides.TryGetValue(normalized, out var existing))
                {
                    existing = new OptionOverride();
                }

                existing.DisplayText = displayText ?? existing.DisplayText;
                existing.DisplayTextPlural = displayTextPlural is null ? existing.DisplayTextPlural : (string.IsNullOrWhiteSpace(displayTextPlural) ? null : displayTextPlural.Trim());
                existing.Hidden = hidden ?? existing.Hidden;
                existing.Position = position ?? existing.Position;
                if (existing.IsEmpty)
                {
                    part.Overrides.Remove(normalized);
                }
                else
                {
                    part.Overrides[normalized] = existing;
                }
            });
            await contentManager.UpdateAsync(tenantItem);
            await contentManager.PublishAsync(tenantItem);
            _byKey.Remove(global.Key);
            var model = (await GetAsync(global.Key, cancellationToken))!;
            return model.Options.First(option => ContentPartListRules.KeyComparer.Equals(option.Key, normalized));
        }

        var listItem = await RequireListItemAsync(listKey);
        var descriptors = await GetOptionFieldsAsync(listItem);
        if (parent is not null
            && ContentPartListRules.ValidateParent((await GetAsync(listKey, cancellationToken))?.Options ?? [], normalized, parent) is { } parentError)
        {
            throw new InvalidOperationException(parentError);
        }

        // The mutation MUST happen inside Alter. As<T>() materializes a part from the
        // item's JSON, so options fetched outside Alter are deserialized copies -
        // changing them updates nothing, and the save silently succeeds having written
        // the original values back.
        ContentItem? option = null;

        listItem.Alter<GroveContentPartListPart>(part =>
        {
            option = part.Options.FirstOrDefault(candidate =>
                ContentPartListRules.KeyComparer.Equals(ContentPartListRules.NormalizeKey(candidate.Get<GroveOptionPart>(typeof(GroveOptionPart).Name)?.Key), normalized));

            if (option is null)
            {
                return;
            }

            if (displayText is not null)
            {
                option.DisplayText = displayText;
                option.Alter<ContentPart>("TitlePart", titlePart => titlePart.Content.Title = displayText);
            }

            if (position is not null || hidden is not null || category is not null || displayTextPlural is not null || value is not null || parent is not null)
            {
                option.Alter<GroveOptionPart>(optionPart =>
                {
                    // Null leaves the level alone; blank moves the option to the top.
                    optionPart.Parent = parent is null
                        ? optionPart.Parent
                        : ContentPartListRules.NormalizeParent(parent);
                    optionPart.Position = position ?? optionPart.Position;
                    optionPart.Hidden = hidden ?? optionPart.Hidden;
                    optionPart.Category = category is null
                        ? optionPart.Category
                        : ContentPartListRules.NormalizeCategory(category);
                    // Null leaves the plural alone; a blank one CLEARS it (back to the
                    // singular fallback) - there is no meaningful "empty plural".
                    optionPart.DisplayTextPlural = displayTextPlural is null
                        ? optionPart.DisplayTextPlural
                        : (string.IsNullOrWhiteSpace(displayTextPlural) ? null : displayTextPlural.Trim());
                    // Same null/blank contract for the machine value.
                    optionPart.Value = value is null
                        ? optionPart.Value
                        : (string.IsNullOrWhiteSpace(value) ? null : value.Trim());
                });
            }

            if (fields is { Count: > 0 })
            {
                WriteFields(option, fields, descriptors);
            }
        });

        if (option is null)
        {
            throw new InvalidOperationException($"Content part list '{listKey}' has no option '{optionKey}'.");
        }

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(ContentPartListRules.NormalizeKey(listKey));

        return ToOptionModel(option, descriptors);
    }

    public async Task<GroveContentPartListModel> ReorderOptionsAsync(string key, IReadOnlyList<string> orderedKeys, CancellationToken cancellationToken = default)
    {
        var merged = await GetAsync(key, cancellationToken)
            ?? throw new InvalidOperationException($"There is no content part list with the key '{key}'.");
        var listItem = await FindListItemAsync(merged.Key)
            ?? await CreateListItemAsync(merged.Key, merged.DisplayText, OptionSources.Module);

        var validation = ContentPartListRules.ValidateReorder(merged.Options.Select(option => option.Key), orderedKeys);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var positionByKey = orderedKeys
            .Select((optionKey, index) => (Key: ContentPartListRules.NormalizeKey(optionKey), Position: index))
            .ToDictionary(entry => entry.Key, entry => entry.Position, ContentPartListRules.KeyComparer);

        var global = await globalLists.GetAsync(merged.Key, cancellationToken);
        if (global is not null)
        {
            listItem.Alter<GroveContentPartListPart>(part =>
            {
                foreach (var option in global.Options)
                {
                    if (positionByKey.TryGetValue(option.Key, out var position))
                    {
                        if (!part.Overrides.TryGetValue(option.Key, out var existing))
                        {
                            existing = new OptionOverride();
                        }

                        existing.Position = position;
                        part.Overrides[option.Key] = existing;
                    }
                }
            });
        }

        // Mutation must happen inside Alter - see UpdateOptionAsync's comment.
        listItem.Alter<GroveContentPartListPart>(part =>
        {
            foreach (var option in part.Options)
            {
                var optionKey = ContentPartListRules.NormalizeKey(option.Get<GroveOptionPart>(typeof(GroveOptionPart).Name)?.Key);
                if (positionByKey.TryGetValue(optionKey, out var position))
                {
                    option.Alter<GroveOptionPart>(optionPart => optionPart.Position = position);
                }
            }
        });

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(ContentPartListRules.NormalizeKey(key));

        return (await GetAsync(key, cancellationToken))!;
    }

    public async Task<GroveContentPartListModel> UpdateListLocksAsync(string key, bool? dataLock, bool? editLock, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(key);

        listItem.Alter<GroveContentPartListPart>(part =>
        {
            part.DataLock = ApplyTenantLock(part.DataLock, dataLock, "categories & data");
            part.EditLock = ApplyTenantLock(part.EditLock, editLock, "editing");
        });

        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);
        _byKey.Remove(ContentPartListRules.NormalizeKey(key));

        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
    }

    public async Task<GroveContentPartListModel> SetOptionFieldLockAsync(string listKey, string fieldName, bool dataLocked, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var descriptors = await GetOptionFieldsAsync(listItem);
        var field = descriptors.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The option type has no custom field named '{fieldName}'.");

        // The designation lives ON the field definition (the option type is the
        // list's), so it travels with the field the way any field setting does.
        await contentDefinitionManager.AlterPartDefinitionAsync(field.PartName, part => part
            .WithField(field.Name, builder => builder
                .MergeSettings<GroveOptionFieldSettings>(settings => settings.DataLocked = dataLocked)));

        _fieldsByType.Clear();
        _byKey.Remove(ContentPartListRules.NormalizeKey(listKey));
        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
    }

    public async Task<IReadOnlyList<string>> GetOptionContentTypesAsync(CancellationToken cancellationToken = default)
    {
        // Eligible option types are the ones carrying GroveOptionPart - the part
        // that holds Key/Category/Position/etc.; without it an option cannot
        // round-trip through this service at all.
        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return [.. definitions
            .Where(definition => definition.Parts.Any(part =>
                string.Equals(part.PartDefinition.Name, nameof(GroveOptionPart), StringComparison.Ordinal)))
            .Select(definition => definition.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<GroveContentPartListModel> SetOptionContentTypeAsync(string listKey, string contentType, CancellationToken cancellationToken = default)
    {
        var listItem = await RequireListItemAsync(listKey);
        var typeName = contentType?.Trim() ?? string.Empty;

        var eligible = await GetOptionContentTypesAsync(cancellationToken);
        if (!eligible.Contains(typeName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{typeName}' is not an option content type - it must exist and carry {nameof(GroveOptionPart)}.");
        }

        // Existing options keep their stored content type; retargeting a populated
        // list would orphan their data behind the new type's field surface and make
        // stale field names fail on update. Migrate-or-recreate is a deliberate,
        // separate decision - refuse the silent version.
        var options = listItem.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name)?.Options ?? [];
        var mismatched = options.FirstOrDefault(option => !string.Equals(option.ContentType, typeName, StringComparison.OrdinalIgnoreCase));
        if (mismatched is not null)
        {
            throw new InvalidOperationException(
                $"The list already has options of type '{mismatched.ContentType}'. The option type can only be set while the list is empty (or already of that type).");
        }

        listItem.Alter<GroveContentPartListPart>(part => part.OptionContentType = typeName);
        await contentManager.UpdateAsync(listItem);
        await contentManager.PublishAsync(listItem);

        _fieldsByType.Clear();
        _byKey.Remove(ContentPartListRules.NormalizeKey(listKey));
        return ToModel(listItem, await GetOptionFieldsAsync(listItem));
    }

    // One mechanism, two authorities: this path is the TENANT authority, so a
    // Module-placed lock is out of reach in both directions.
    private static string ApplyTenantLock(string current, bool? wanted, string lockName)
    {
        if (wanted is null)
        {
            return current;
        }

        if (string.Equals(current, ContentPartListLockSources.Module, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {lockName} lock on this list was placed by the owning module and cannot be changed from the tenant.");
        }

        return wanted.Value ? ContentPartListLockSources.Tenant : ContentPartListLockSources.None;
    }

    public async Task DeleteListAsync(string key, CancellationToken cancellationToken = default)
    {
        if (await globalLists.GetAsync(ContentPartListRules.NormalizeKey(key), cancellationToken) is not null)
        {
            throw new InvalidOperationException($"The content part list '{key}' is global reference data and cannot be deleted. Hide its options instead.");
        }

        var listItem = await RequireListItemAsync(key);

        // A module-seeded list's key is that module's contract with code; deleting it
        // would break the consuming module until the next reseed silently recreated an
        // empty list. Tenants hide module options instead.
        var source = listItem.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name)?.Source ?? OptionSources.Tenant;
        if (string.Equals(source, OptionSources.Module, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The content part list '{key}' is provided by a module and cannot be deleted. Hide its options instead.");
        }

        // The options live inside this item's own document, so removing the list
        // removes them with it - there are no orphan Option items to sweep.
        await contentManager.RemoveAsync(listItem);
        _byKey.Remove(ContentPartListRules.NormalizeKey(key));
    }

    public async Task AttachToContentTypeAsync(string listKey, string contentType, string fieldName, string? displayName = null, Action<OptionPickerFieldSettings>? configure = null, CancellationToken cancellationToken = default)
    {
        if (await GetAsync(listKey, cancellationToken) is null)
        {
            throw new InvalidOperationException($"There is no content part list with the key '{listKey}'.");
        }

        var sourceKey = OptionSourceKeys.ForContentPartList(ContentPartListRules.NormalizeKey(listKey));

        // WithField updates an EXISTING field's settings but keeps its original type,
        // so converting a field that used to be (say) a TextField needs it removed
        // first - otherwise the settings say "option picker" while the field is still
        // text. Stored content is untouched by this: the field's own data stays in the
        // item's document, which is what lets readers fall back to the previous shape
        // for content written before the conversion.
        var existing = await contentDefinitionManager.GetPartDefinitionAsync(contentType);
        var current = existing?.Fields.FirstOrDefault(field =>
            string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        var position = current?.Settings?["ContentPartFieldSettings"]?["Position"]?.ToString();

        // The remove/re-add conversion would drop every other settings section on the
        // field (a definition lock the declaring migration placed, a visibility
        // condition): carry them across, as the field-type editor does.
        var preserved = current?.Settings?.DeepClone()?.AsObject();
        preserved?.Remove("ContentPartFieldSettings");
        preserved?.Remove(nameof(OptionPickerFieldSettings));

        if (current is not null && !string.Equals(current.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal))
        {
            await contentDefinitionManager.AlterPartDefinitionAsync(contentType, part => part.RemoveField(fieldName));
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(contentType, part => part
            .WithField(fieldName, field =>
            {
                if (preserved is { Count: > 0 })
                {
                    field.MergeSettings(preserved);
                }

                field
                    .OfType(nameof(OptionPickerField))
                    .WithDisplayName(displayName ?? fieldName)
                    .MergeSettings<OptionPickerFieldSettings>(settings =>
                    {
                        settings.SourceKey = sourceKey;
                        settings.Multiple = false;
                        // Caller-shaped settings (cascade filters, sort columns, ...)
                        // apply after the defaults, and on every reattach - the
                        // declaring migration owns them, so they are re-asserted.
                        configure?.Invoke(settings);
                    });

                // Keep the field where the editor already put it.
                if (!string.IsNullOrWhiteSpace(position))
                {
                    field.WithPosition(position);
                }
            }));
    }

    private async Task<ContentItem?> FindListItemAsync(string normalizedKey)
    {
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == GroveContentPartListMigrations.ContentPartListContentType && index.Latest)
            .ListAsync();

        return items.FirstOrDefault(item =>
            ContentPartListRules.KeyComparer.Equals(
                ContentPartListRules.NormalizeKey(item.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name)?.Key),
                normalizedKey));
    }

    private async Task<ContentItem> RequireListItemAsync(string listKey)
    {
        var normalized = ContentPartListRules.NormalizeKey(listKey);
        return await FindListItemAsync(normalized)
            ?? throw new InvalidOperationException($"There is no content part list with the key '{listKey}'.");
    }

    private async Task<ContentItem> CreateListItemAsync(string key, string displayText, string source)
    {
        var item = await contentManager.NewAsync(GroveContentPartListMigrations.ContentPartListContentType);
        item.DisplayText = displayText;
        item.Alter<ContentPart>("TitlePart", part => part.Content.Title = displayText);
        item.Alter<GroveContentPartListPart>(part =>
        {
            part.Key = key;
            part.Source = source;
            part.OptionContentType = GroveContentPartListMigrations.OptionContentType;
        });

        await contentManager.CreateAsync(item, VersionOptions.Draft);
        await contentManager.PublishAsync(item);
        return item;
    }

    private async Task<ContentItem> AppendOptionAsync(ContentItem listItem, string key, string displayText, int position, string source, string? category = null, string? displayTextPlural = null, string? value = null, string? parent = null)
    {
        // New options take the LIST's option content type (custom data fields live
        // on it); the shared Option type is only the blank-value fallback.
        var optionType = listItem.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name)?.OptionContentType;
        var option = await contentManager.NewAsync(
            string.IsNullOrWhiteSpace(optionType) ? GroveContentPartListMigrations.OptionContentType : optionType);
        option.DisplayText = displayText;
        option.Alter<ContentPart>("TitlePart", part => part.Content.Title = displayText);
        option.Alter<GroveOptionPart>(part =>
        {
            part.Key = key;
            part.Source = source;
            part.Position = position;
            part.Category = ContentPartListRules.NormalizeCategory(category);
            part.DisplayTextPlural = string.IsNullOrWhiteSpace(displayTextPlural) ? null : displayTextPlural.Trim();
            part.Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            part.Parent = ContentPartListRules.NormalizeParent(parent);
            part.ContentPartListContentItemId = listItem.ContentItemId;
        });

        listItem.Alter<GroveContentPartListPart>(part => part.Options.Add(option));
        return option;
    }

    // The list's custom-field descriptors: every field on every part of its option
    // content type, with the admin's per-field lock designation. Null-safe on the
    // type name because pre-existing lists always used the shared Option type.
    private async Task<GroveOptionFieldModel[]> GetOptionFieldsAsync(ContentItem listItem)
    {
        var optionContentType = listItem.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name)?.OptionContentType;
        var typeName = string.IsNullOrWhiteSpace(optionContentType)
            ? GroveContentPartListMigrations.OptionContentType
            : optionContentType;

        if (_fieldsByType.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        var type = await contentDefinitionManager.GetTypeDefinitionAsync(typeName);
        GroveOptionFieldModel[] fields = type is null
            ? []
            :
            [
                .. type.Parts.SelectMany(typePart => typePart.PartDefinition.Fields.Select(field => new GroveOptionFieldModel(
                    field.Name,
                    field.GetSettings<ContentPartFieldSettings>()?.DisplayName is { Length: > 0 } displayName ? displayName : field.Name,
                    typePart.PartDefinition.Name,
                    field.FieldDefinition.Name,
                    field.GetSettings<GroveOptionFieldSettings>()?.DataLocked ?? false))),
            ];

        _fieldsByType[typeName] = fields;
        return fields;
    }

    // Writes custom field values onto the option item. Must run where the option is
    // LIVE - inside the list's Alter for updates (see UpdateOptionAsync's comment),
    // or on a freshly created option before the list saves.
    private static void WriteFields(ContentItem option, IReadOnlyDictionary<string, string?> values, GroveOptionFieldModel[] descriptors)
    {
        foreach (var (name, raw) in values)
        {
            var field = descriptors.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The option type has no custom field named '{name}'.");

            // Blank clears (ToFieldNode returns null), same contract as plural/Value.
            var node = OptionFieldAccessor.ToFieldNode(field.Type, field.Name, raw);
            option.Alter<ContentPart>(field.PartName, part => part.Content[field.Name] = node);
        }
    }

    // The merged view of a global-backed list: standard rows (deterministic ids, Module
    // source, overrides applied) plus the tenant item's own additions.
    private static GroveContentPartListModel Merge(GlobalList global, ContentItem? tenantItem, GroveOptionFieldModel[] fields)
    {
        var tenantPart = tenantItem?.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name);
        var overrides = tenantPart?.Overrides ?? new Dictionary<string, OptionOverride>(StringComparer.OrdinalIgnoreCase);

        var standard = global.Options.Select(option =>
        {
            overrides.TryGetValue(option.Key, out var over);
            return new GroveOptionModel(
                GlobalLists.OptionId(global.Key, option.Key),
                ContentPartListRules.NormalizeKey(option.Key),
                over?.DisplayText ?? option.DisplayText,
                OptionSources.Module,
                over?.Hidden ?? false,
                over?.Position ?? option.Position,
                ContentPartListRules.NormalizeCategory(option.Category),
                string.IsNullOrWhiteSpace(over?.DisplayTextPlural ?? option.DisplayTextPlural) ? null : (over?.DisplayTextPlural ?? option.DisplayTextPlural)!.Trim(),
                string.IsNullOrWhiteSpace(option.Value) ? null : option.Value.Trim(),
                null,
                ContentPartListRules.NormalizeParent(option.Parent));
        });
        var additions = (tenantPart?.Options ?? []).Select(option => ToOptionModel(option, fields));

        return new GroveContentPartListModel(
            tenantItem?.ContentItemId ?? GlobalLists.OptionId(global.Key, string.Empty),
            ContentPartListRules.NormalizeKey(global.Key),
            global.DisplayText,
            OptionSources.Module,
            global.DataLock ? ContentPartListLockSources.Module : (string.IsNullOrEmpty(tenantPart?.DataLock) ? ContentPartListLockSources.None : tenantPart.DataLock),
            string.IsNullOrEmpty(tenantPart?.EditLock) ? ContentPartListLockSources.None : tenantPart.EditLock,
            [.. ContentPartListRules.Ordered(standard.Concat(additions))],
            fields,
            string.IsNullOrWhiteSpace(tenantPart?.OptionContentType) ? GroveContentPartListMigrations.OptionContentType : tenantPart.OptionContentType);
    }

    private static GroveContentPartListModel ToModel(ContentItem item, GroveOptionFieldModel[]? fields = null)
    {
        var listPart = item.Get<GroveContentPartListPart>(typeof(GroveContentPartListPart).Name);
        var options = listPart?.Options ?? [];

        return new GroveContentPartListModel(
            item.ContentItemId,
            ContentPartListRules.NormalizeKey(listPart?.Key),
            item.DisplayText ?? string.Empty,
            listPart?.Source ?? OptionSources.Tenant,
            string.IsNullOrEmpty(listPart?.DataLock) ? ContentPartListLockSources.None : listPart.DataLock,
            string.IsNullOrEmpty(listPart?.EditLock) ? ContentPartListLockSources.None : listPart.EditLock,
            [.. ContentPartListRules.Ordered(options.Select(option => ToOptionModel(option, fields)))],
            fields ?? [],
            string.IsNullOrWhiteSpace(listPart?.OptionContentType)
                ? GroveContentPartListMigrations.OptionContentType
                : listPart.OptionContentType);
    }

    private static GroveOptionModel ToOptionModel(ContentItem option, GroveOptionFieldModel[]? fields = null)
    {
        var part = option.Get<GroveOptionPart>(typeof(GroveOptionPart).Name);

        IReadOnlyDictionary<string, string?>? fieldValues = null;
        if (fields is { Length: > 0 })
        {
            // Same one-shot conversion as the content-item provider: Content is a
            // dynamic view over the item's JSON, so read it as a JsonObject once.
            JsonObject? document = option.Content is JsonDynamicObject dynamicObject
                ? (JsonObject)dynamicObject
                : option.Content as JsonObject;

            fieldValues = fields.ToDictionary(
                field => field.Name,
                field => OptionFieldAccessor.ReadValue(document, field.PartName, field.Name),
                StringComparer.OrdinalIgnoreCase);
        }

        return new GroveOptionModel(
            option.ContentItemId,
            ContentPartListRules.NormalizeKey(part?.Key),
            option.DisplayText ?? string.Empty,
            part?.Source ?? OptionSources.Tenant,
            part?.Hidden ?? false,
            part?.Position ?? 0,
            ContentPartListRules.NormalizeCategory(part?.Category),
            string.IsNullOrWhiteSpace(part?.DisplayTextPlural) ? null : part.DisplayTextPlural.Trim(),
            string.IsNullOrWhiteSpace(part?.Value) ? null : part.Value.Trim(),
            fieldValues,
            ContentPartListRules.NormalizeParent(part?.Parent));
    }
}
