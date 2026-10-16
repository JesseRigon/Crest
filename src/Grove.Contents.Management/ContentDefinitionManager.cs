using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Records;
using Grove.Contents.Types.Events;
using Grove.Data.Documents;
using Grove.Modules;

namespace Grove.Contents.Management;

public class ContentDefinitionManager : IContentDefinitionManager
{
    private const string CacheKey = nameof(ContentDefinitionManager);

    private readonly IContentDefinitionStore _contentDefinitionStore;
    private readonly IEnumerable<IContentDefinitionHandler> _handlers;
    private readonly ILogger _logger;
    private readonly IMemoryCache _memoryCache;

    private readonly ConcurrentDictionary<string, ContentTypeDefinition> _cachedTypeDefinitions;
    private readonly ConcurrentDictionary<string, ContentFieldSetDefinition> _cachedFieldSetDefinitions;

    private readonly Dictionary<string, ContentTypeDefinition> _scopedTypeDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ContentFieldSetDefinition> _scopedFieldSetDefinitions = new(StringComparer.OrdinalIgnoreCase);

    public ContentDefinitionManager(
        IContentDefinitionStore contentDefinitionStore,
        IEnumerable<IContentDefinitionHandler> handlers,
        ILogger<ContentDefinitionManager> logger,
        IMemoryCache memoryCache)
    {
        _contentDefinitionStore = contentDefinitionStore;
        _handlers = handlers;
        _logger = logger;
        _memoryCache = memoryCache;

        _cachedTypeDefinitions = _memoryCache.GetOrCreate("TypeDefinitions", entry => new ConcurrentDictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase));
        _cachedFieldSetDefinitions = _memoryCache.GetOrCreate("FieldSetDefinitions", entry => new ConcurrentDictionary<string, ContentFieldSetDefinition>(StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<ContentTypeDefinition>> LoadTypeDefinitionsAsync()
    {
        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        return document.ContentTypeDefinitionRecords.Select(type => LoadTypeDefinition(document, type.Name)).ToList();
    }

    public async Task<IEnumerable<ContentTypeDefinition>> ListTypeDefinitionsAsync()
    {
        var document = await _contentDefinitionStore.GetContentDefinitionAsync();

        CheckDocumentIdentifier(document);

        return document.ContentTypeDefinitionRecords.Select(type => GetTypeDefinition(document, type.Name)).ToList();
    }

    public async Task<IEnumerable<ContentFieldSetDefinition>> LoadFieldSetDefinitionsAsync()
    {
        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        return document.ContentFieldSetDefinitionRecords.Select(part => LoadFieldSetDefinition(document, part.Name)).ToList();
    }

    public async Task<IEnumerable<ContentFieldSetDefinition>> ListFieldSetDefinitionsAsync()
    {
        var document = await _contentDefinitionStore.GetContentDefinitionAsync();

        CheckDocumentIdentifier(document);

        return document.ContentFieldSetDefinitionRecords.Select(part => GetFieldSetDefinition(document, part.Name)).ToList();
    }

    public async Task<ContentTypeDefinition> LoadTypeDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (_scopedTypeDefinitions.TryGetValue(name, out var typeDefinition))
        {
            return typeDefinition;
        }

        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        return LoadTypeDefinition(document, name);
    }

    public async Task<ContentTypeDefinition> GetTypeDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await _contentDefinitionStore.GetContentDefinitionAsync();

        CheckDocumentIdentifier(document);

        return GetTypeDefinition(document, name);
    }

    public async Task<ContentFieldSetDefinition> LoadFieldSetDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (_scopedFieldSetDefinitions.TryGetValue(name, out var partDefinition))
        {
            return partDefinition;
        }

        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        return LoadFieldSetDefinition(document, name);
    }

    public async Task<ContentFieldSetDefinition> GetFieldSetDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await _contentDefinitionStore.GetContentDefinitionAsync();

        CheckDocumentIdentifier(document);

        return GetFieldSetDefinition(document, name);
    }

    public async Task DeleteTypeDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        var record = document.ContentTypeDefinitionRecords.FirstOrDefault(record => record.Name.EqualsOrdinalIgnoreCase(name));

        // Deletes the content type record associated.
        if (record is not null)
        {
            document.ContentTypeDefinitionRecords.Remove(record);
            await UpdateContentDefinitionRecordAsync(document);
        }
    }

    public async Task DeleteFieldSetDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        // Remove parts from current types.
        var typeDefinitions = document.ContentTypeDefinitionRecords
            .Select(type => LoadTypeDefinition(document, type.Name)).ToList();

        var typesWithFieldSet = typeDefinitions
            .Where(typeDefinition => typeDefinition.FieldSets
                .Any(part => part.FieldSetDefinition.Name.EqualsOrdinalIgnoreCase(name)));

        foreach (var typeDefinition in typesWithFieldSet)
        {
            await this.AlterTypeDefinitionAsync(typeDefinition.Name, builder => Task.FromResult(builder.RemoveFieldSet(name)));
        }

        // Delete part.
        var record = document.ContentFieldSetDefinitionRecords.FirstOrDefault(part => part.Name.EqualsOrdinalIgnoreCase(name));
        if (record is not null)
        {
            document.ContentFieldSetDefinitionRecords.Remove(record);
            await UpdateContentDefinitionRecordAsync(document);
        }
    }

    public async Task StoreTypeDefinitionAsync(ContentTypeDefinition contentTypeDefinition)
    {
        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        Apply(contentTypeDefinition, Acquire(document, contentTypeDefinition));

        await UpdateContentDefinitionRecordAsync(document);
    }

    public async Task StoreFieldSetDefinitionAsync(ContentFieldSetDefinition contentFieldSetDefinition)
    {
        var document = await _contentDefinitionStore.LoadContentDefinitionAsync();

        Apply(contentFieldSetDefinition, Acquire(document, contentFieldSetDefinition));

        await UpdateContentDefinitionRecordAsync(document);
    }

    public async Task<string> GetIdentifierAsync()
        => (await _contentDefinitionStore.GetContentDefinitionAsync()).Identifier;

    private ContentTypeDefinition LoadTypeDefinition(ContentDefinitionRecord document, string name) =>
        _scopedTypeDefinitions.TryGetValue(name, out var typeDefinition)
        ? typeDefinition
        : _scopedTypeDefinitions[name] = Build(
            name,
            document.ContentTypeDefinitionRecords.FirstOrDefault(type => type.Name.EqualsOrdinalIgnoreCase(name)),
            document.ContentFieldSetDefinitionRecords);

    private ContentTypeDefinition GetTypeDefinition(ContentDefinitionRecord document, string name) =>
        _cachedTypeDefinitions.GetOrAdd(name, name => Build(
            name,
            document.ContentTypeDefinitionRecords.FirstOrDefault(type => type.Name.EqualsOrdinalIgnoreCase(name)),
            document.ContentFieldSetDefinitionRecords));

    private ContentFieldSetDefinition LoadFieldSetDefinition(ContentDefinitionRecord document, string name) =>
        !_scopedFieldSetDefinitions.TryGetValue(name, out var partDefinition)
        ? _scopedFieldSetDefinitions[name] = Build(
            name,
            document.ContentFieldSetDefinitionRecords.FirstOrDefault(part => part.Name.EqualsOrdinalIgnoreCase(name)))
        : partDefinition;

    private ContentFieldSetDefinition GetFieldSetDefinition(ContentDefinitionRecord document, string name) =>
        _cachedFieldSetDefinitions.GetOrAdd(name, name => Build(
            name,
            document.ContentFieldSetDefinitionRecords.FirstOrDefault(record => record.Name.EqualsOrdinalIgnoreCase(name))));

    private static ContentTypeDefinitionRecord Acquire(
        ContentDefinitionRecord document,
        ContentTypeDefinition contentTypeDefinition)
    {
        var result = document.ContentTypeDefinitionRecords
            .FirstOrDefault(type => type.Name.EqualsOrdinalIgnoreCase(contentTypeDefinition.Name));

        if (result is null)
        {
            result = new ContentTypeDefinitionRecord
            {
                Name = contentTypeDefinition.Name,
                DisplayName = contentTypeDefinition.DisplayName,
            };

            document.ContentTypeDefinitionRecords.Add(result);
        }

        return result;
    }

    private static ContentFieldSetDefinitionRecord Acquire(
        ContentDefinitionRecord document,
        ContentFieldSetDefinition contentFieldSetDefinition)
    {
        var result = document.ContentFieldSetDefinitionRecords
            .FirstOrDefault(part => part.Name.EqualsOrdinalIgnoreCase(contentFieldSetDefinition.Name));

        if (result is null)
        {
            result = new ContentFieldSetDefinitionRecord
            {
                Name = contentFieldSetDefinition.Name,
            };

            document.ContentFieldSetDefinitionRecords.Add(result);
        }

        return result;
    }

    private static void Apply(ContentTypeDefinition model, ContentTypeDefinitionRecord record)
    {
        record.DisplayName = model.DisplayName;
        record.Settings = model.Settings;
        record.EmbeddedContentDefinitionRecords = [.. model.Embeddings.Select(embedding => embedding.ToRecord())];

        var toRemove = record.ContentTypeFieldSetDefinitionRecords
            .Where(typeFieldSetDefinitionRecord => !model.FieldSets
                .Any(typeFieldSet => typeFieldSet.Name.EqualsOrdinalIgnoreCase(typeFieldSetDefinitionRecord.Name)))
            .ToArray();

        foreach (var remove in toRemove)
        {
            record.ContentTypeFieldSetDefinitionRecords.Remove(remove);
        }

        foreach (var part in model.FieldSets)
        {
            var typeFieldSetRecord = record.ContentTypeFieldSetDefinitionRecords
                .FirstOrDefault(typeFieldSet => typeFieldSet.Name.EqualsOrdinalIgnoreCase(part.Name));

            if (typeFieldSetRecord is null)
            {
                typeFieldSetRecord = new ContentTypeFieldSetDefinitionRecord
                {
                    FieldSetName = part.FieldSetDefinition.Name,
                    Name = part.Name,
                    Settings = part.Settings,
                };

                record.ContentTypeFieldSetDefinitionRecords.Add(typeFieldSetRecord);
            }

            Apply(part, typeFieldSetRecord);
        }
    }

    private static void Apply(ContentTypeFieldSetDefinition model, ContentTypeFieldSetDefinitionRecord record)
        => record.Settings = model.Settings;

    private static void Apply(ContentFieldSetDefinition model, ContentFieldSetDefinitionRecord record)
    {
        record.Settings = model.Settings;

        var toRemove = record.ContentFieldDefinitionRecords
            .Where(partFieldDefinitionRecord => !model.Fields
                .Any(partField => partField.Name.EqualsOrdinalIgnoreCase(partFieldDefinitionRecord.Name)))
            .ToArray();

        foreach (var remove in toRemove)
        {
            record.ContentFieldDefinitionRecords.Remove(remove);
        }

        foreach (var field in model.Fields)
        {
            var partFieldRecord = record.ContentFieldDefinitionRecords
                .FirstOrDefault(partField => partField.Name.EqualsOrdinalIgnoreCase(field.Name));

            if (partFieldRecord is null)
            {
                if (field.FieldTypeDefinition is null)
                {
                    throw new InvalidOperationException(
                        $"The '{field.Name}' field in '{model.Name}' part was defined without a specified type." +
                        " Please review the migration and explicitly specify the field type.");
                }

                partFieldRecord = new ContentFieldDefinitionRecord
                {
                    FieldName = field.FieldTypeDefinition.Name,
                    Name = field.Name,
                };

                record.ContentFieldDefinitionRecords.Add(partFieldRecord);
            }

            Apply(field, partFieldRecord);
        }
    }

    private static void Apply(ContentFieldDefinition model, ContentFieldDefinitionRecord record)
        => record.Settings = model.Settings;

    private ContentTypeDefinition Build(
        string name,
        ContentTypeDefinitionRecord source,
        IList<ContentFieldSetDefinitionRecord> partDefinitionRecords)
    {
        var context = new ContentTypeBuildingContext(name, source);

        _handlers.Invoke((handler, ctx) => handler.ContentTypeBuilding(ctx), context, _logger);

        if (context.Record is null)
        {
            return null;
        }

        return new ContentTypeDefinition(
            context.Record.Name,
            context.Record.DisplayName,
            context.Record.ContentTypeFieldSetDefinitionRecords.Select(typeFieldSet => Build(context.Record.Name,
                typeFieldSet,
                partDefinitionRecords.FirstOrDefault(part => part.Name.EqualsOrdinalIgnoreCase(typeFieldSet.FieldSetName)))),
            context.Record.Settings,
            (context.Record.EmbeddedContentDefinitionRecords ?? []).Select(embedding => new EmbeddedContentDefinition(embedding)));
    }

    private ContentTypeFieldSetDefinition Build(
        string name,
        ContentTypeFieldSetDefinitionRecord source,
        ContentFieldSetDefinitionRecord partDefinitionRecord)
    {
        var context = new ContentTypeFieldSetBuildingContext(name, source);

        _handlers.Invoke((handler, ctx) => handler.ContentTypeFieldSetBuilding(ctx), context, _logger);

        if (context.Record is null)
        {
            return null;
        }

        return new ContentTypeFieldSetDefinition(
            context.Record.Name,
            Build(context.Record.Name, partDefinitionRecord) ?? new ContentFieldSetDefinition(context.Record.FieldSetName, [], []),
            context.Record.Settings);
    }

    private ContentFieldSetDefinition Build(string name, ContentFieldSetDefinitionRecord source)
    {
        var context = new ContentFieldSetBuildingContext(name, source);

        _handlers.Invoke((handler, ctx) => handler.ContentFieldSetBuilding(ctx), context, _logger);

        if (context.Record is null)
        {
            return null;
        }

        return new ContentFieldSetDefinition(
            context.Record.Name,
            context.Record.ContentFieldDefinitionRecords.Select(x => Build(context.Record.Name, x)),
            context.Record.Settings);
    }

    private ContentFieldDefinition Build(string name, ContentFieldDefinitionRecord source)
    {
        var context = new ContentFieldBuildingContext(name, source);

        _handlers.Invoke((handler, ctx) => handler.ContentFieldBuilding(ctx), context, _logger);

        if (context.Record is null)
        {
            return null;
        }

        return new ContentFieldDefinition(
            Build(new ContentFieldTypeDefinitionRecord
            {
                Name = context.Record.FieldName,
            }),
            context.Record.Name,
            context.Record.Settings);
    }

    private static ContentFieldTypeDefinition Build(ContentFieldTypeDefinitionRecord source)
    {
        if (source is null)
        {
            return null;
        }

        return new ContentFieldTypeDefinition(source.Name);
    }

    private async Task UpdateContentDefinitionRecordAsync(ContentDefinitionRecord document)
    {
        await _contentDefinitionStore.SaveContentDefinitionAsync(document);

        // If multiple updates in the same scope, types and parts may need to be rebuilt.
        _scopedTypeDefinitions.Clear();
        _scopedFieldSetDefinitions.Clear();
    }

    /// <summary>
    /// Checks the document identifier and then clears the cached built definitions if it has changed.
    /// </summary>
    private void CheckDocumentIdentifier(ContentDefinitionRecord document)
    {
        if (!_memoryCache.TryGetValue<Document>(CacheKey, out var cacheEntry) ||
            cacheEntry.Identifier != document.Identifier)
        {
            cacheEntry = new Document()
            {
                Identifier = document.Identifier,
            };

            _cachedTypeDefinitions.Clear();
            _cachedFieldSetDefinitions.Clear();

            _memoryCache.Set(CacheKey, cacheEntry);
        }
    }
}
