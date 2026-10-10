using Crest.DataLocalization.Models;

namespace Crest.DataLocalization.Services;

public interface ITranslationsManager
{
    Task<TranslationsDocument> LoadTranslationsDocumentAsync();

    Task<TranslationsDocument> GetTranslationsDocumentAsync();

    /// <summary>Removes every translation of a culture.</summary>
    Task RemoveTranslationAsync(string name);

    /// <summary>Replaces a culture's whole translation list.</summary>
    Task UpdateTranslationAsync(string name, IEnumerable<Translation> translations);

    /// <summary>
    /// Sets one translation of a culture, replacing the entry for the same context and key and
    /// leaving every other entry as it is; a blank value removes the entry.
    /// </summary>
    Task SetTranslationAsync(string culture, string context, string key, string value);

    /// <summary>
    /// Removes the given keys under a context from every culture: for when the translated thing
    /// itself is deleted, so its translations do not linger as orphans no editor row can reach.
    /// </summary>
    Task RemoveTranslationsAsync(string context, IReadOnlyCollection<string> keys);

    /// <summary>Removes every entry under a context from every culture.</summary>
    Task RemoveContextAsync(string context);
}
