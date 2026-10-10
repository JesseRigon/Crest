using Microsoft.Extensions.Caching.Memory;
using Crest.Data.Documents;
using Crest.DataLocalization.Models;
using Crest.Documents;

namespace Crest.DataLocalization.Services;

public class TranslationsManager : ITranslationsManager
{
    // Cache key prefix used by DataResourceManager for data localizations.
    private const string DataCultureDictionaryCacheKeyPrefix = "DataCultureDictionary-";

    private readonly IDocumentManager<TranslationsDocument> _documentManager;
    private readonly IDocumentStore _documentStore;
    private readonly IMemoryCache _memoryCache;

    public TranslationsManager(
        IDocumentManager<TranslationsDocument> documentManager,
        IDocumentStore documentStore,
        IMemoryCache memoryCache)
    {
        _documentManager = documentManager;
        _documentStore = documentStore;
        _memoryCache = memoryCache;
    }

    public Task<TranslationsDocument> LoadTranslationsDocumentAsync() => _documentManager.GetOrCreateMutableAsync();

    public Task<TranslationsDocument> GetTranslationsDocumentAsync() => _documentManager.GetOrCreateImmutableAsync();

    public async Task RemoveTranslationAsync(string name)
    {
        var document = await LoadTranslationsDocumentAsync();

        document.Translations.Remove(name);

        await UpdateAsync(document, name);
    }

    public async Task UpdateTranslationAsync(string name, IEnumerable<Translation> translations)
    {
        var document = await LoadTranslationsDocumentAsync();

        document.Translations[name] = translations.ToList();

        await UpdateAsync(document, name);
    }

    public async Task SetTranslationAsync(string culture, string context, string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(culture);
        ArgumentException.ThrowIfNullOrEmpty(context);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var document = await LoadTranslationsDocumentAsync();
        var entries = document.Translations.TryGetValue(culture, out var current) ? current.ToList() : [];

        entries.RemoveAll(entry =>
            string.Equals(entry.Context, context, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Key, key, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new Translation { Context = context, Key = key, Value = value.Trim() });
        }

        document.Translations[culture] = entries;

        await UpdateAsync(document, culture);
    }

    public async Task RemoveTranslationsAsync(string context, IReadOnlyCollection<string> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        await RemoveWhereAsync(entry =>
            string.Equals(entry.Context, context, StringComparison.OrdinalIgnoreCase) &&
            keys.Contains(entry.Key, StringComparer.Ordinal));
    }

    public Task RemoveContextAsync(string context)
        => RemoveWhereAsync(entry => string.Equals(entry.Context, context, StringComparison.OrdinalIgnoreCase));

    private async Task RemoveWhereAsync(Func<Translation, bool> orphaned)
    {
        var document = await LoadTranslationsDocumentAsync();
        var changed = new List<string>();

        foreach (var culture in document.Translations.Keys.ToList())
        {
            var entries = document.Translations[culture].ToList();
            if (entries.RemoveAll(entry => orphaned(entry)) > 0)
            {
                document.Translations[culture] = entries;
                changed.Add(culture);
            }
        }

        if (changed.Count > 0)
        {
            await UpdateAsync(document, changed.ToArray());
        }
    }

    private async Task UpdateAsync(TranslationsDocument document, params string[] cultures)
    {
        await _documentManager.UpdateAsync(document);

        // The document write commits when the scope ends, so an eviction now alone would let a
        // read in between re-prime the dictionary from the old document and serve it until the
        // next save. Evict now for this scope's own reads, and again after the commit.
        ClearCultureDictionaryCache(cultures);
        _documentStore.AfterCommitSuccess<TranslationsDocument>(() =>
        {
            ClearCultureDictionaryCache(cultures);
            return Task.CompletedTask;
        });
    }

    private void ClearCultureDictionaryCache(IEnumerable<string> cultures)
    {
        foreach (var culture in cultures)
        {
            _memoryCache.Remove(DataCultureDictionaryCacheKeyPrefix + culture);
        }
    }
}
