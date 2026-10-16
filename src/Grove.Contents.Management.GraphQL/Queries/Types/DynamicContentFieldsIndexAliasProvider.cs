using Microsoft.Extensions.Caching.Memory;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Types.Events;

namespace Grove.Contents.Management.GraphQL.Queries.Types;

public sealed class DynamicContentFieldsIndexAliasProvider : IIndexAliasProvider, IContentDefinitionEventHandler
{
    private static readonly string s_cacheKey = nameof(DynamicContentFieldsIndexAliasProvider);

    private readonly IEnumerable<IContentFieldProvider> _contentFieldProviders;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IMemoryCache _memoryCache;

    public DynamicContentFieldsIndexAliasProvider(
        IEnumerable<IContentFieldProvider> contentFieldProviders,
        IContentDefinitionManager contentDefinitionManager,
        IMemoryCache memoryCache)
    {
        _contentFieldProviders = contentFieldProviders;
        _contentDefinitionManager = contentDefinitionManager;
        _memoryCache = memoryCache;
    }

    public async ValueTask<IEnumerable<IndexAlias>> GetAliasesAsync()
    {
        return await _memoryCache.GetOrCreateAsync(s_cacheKey, async _ => await GetAliasesInternalAsync());
    }

    private async ValueTask<IEnumerable<IndexAlias>> GetAliasesInternalAsync()
    {
        var aliases = new List<IndexAlias>();

        var types = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var parts = types.SelectMany(t => t.FieldSets);

        foreach (var part in parts)
        {
            foreach (var field in part.FieldSetDefinition.Fields)
            {
                foreach (var fieldProvider in _contentFieldProviders)
                {
                    if (!fieldProvider.HasFieldIndex(field))
                    {
                        continue;
                    }

                    var fieldIndex = fieldProvider.GetFieldIndex(field);

                    if (fieldIndex is null)
                    {
                        continue;
                    }

                    aliases.Add(new IndexAlias
                    {
                        Alias = fieldIndex.AliasName,
                        Index = fieldIndex.IndexType.Name,
                        IndexType = fieldIndex.IndexType,
                        IsPartial = true,
                    });

                    aliases.Add(new IndexAlias
                    {
                        Alias = $"{fieldIndex.AliasName}:ContentFieldSet",
                        Index = fieldIndex.IndexType.Name,
                        IndexType = fieldIndex.IndexType,
                        IsPartial = true,
                    });

                    aliases.Add(new IndexAlias
                    {
                        Alias = $"{fieldIndex.AliasName}:ContentField",
                        Index = fieldIndex.IndexType.Name,
                        IndexType = fieldIndex.IndexType,
                        IsPartial = true,
                    });

                    break;
                }
            }
        }

        return aliases;
    }

    private void InvalidateInternal()
        => _memoryCache.Remove(s_cacheKey);

    public void ContentFieldAttached(ContentFieldAttachedContext context)
        => InvalidateInternal();

    public void ContentFieldDetached(ContentFieldDetachedContext context)
        => InvalidateInternal();

    public void ContentFieldSetAttached(ContentFieldSetAttachedContext context)
        => InvalidateInternal();

    public void ContentFieldSetCreated(ContentFieldSetCreatedContext context)
        => InvalidateInternal();

    public void ContentFieldSetDetached(ContentFieldSetDetachedContext context)
        => InvalidateInternal();

    public void ContentFieldSetImported(ContentFieldSetImportedContext context)
        => InvalidateInternal();

    public void ContentFieldSetRemoved(ContentFieldSetRemovedContext context)
        => InvalidateInternal();

    public void ContentTypeCreated(ContentTypeCreatedContext context)
        => InvalidateInternal();

    public void ContentTypeImported(ContentTypeImportedContext context)
        => InvalidateInternal();

    public void ContentTypeRemoved(ContentTypeRemovedContext context)
        => InvalidateInternal();

    public void ContentTypeUpdated(ContentTypeUpdatedContext context)
        => InvalidateInternal();

    public void ContentFieldSetUpdated(ContentFieldSetUpdatedContext context)
        => InvalidateInternal();

    public void ContentTypeFieldSetUpdated(ContentTypeFieldSetUpdatedContext context)
        => InvalidateInternal();

    public void ContentFieldUpdated(ContentFieldUpdatedContext context)
        => InvalidateInternal();

    public void ContentTypeImporting(ContentTypeImportingContext context)
    {
    }

    public void ContentFieldSetImporting(ContentFieldSetImportingContext context)
    {
    }
}
