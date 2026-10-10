using Crest.DataLocalization.Models;

namespace Crest.DataLocalization.Services;

public interface ITranslationsManager
{
    Task<TranslationsDocument> LoadTranslationsDocumentAsync();

    Task<TranslationsDocument> GetTranslationsDocumentAsync();

    Task RemoveTranslationAsync(string name);

    Task UpdateTranslationAsync(string name, IEnumerable<Translation> translations);
}
