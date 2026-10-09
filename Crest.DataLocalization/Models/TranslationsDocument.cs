using Crest.Data.Documents;

namespace Crest.DataLocalization.Models;

public class TranslationsDocument : Document
{
    public TranslationsDocument()
    {
        Translations = new Dictionary<string, IEnumerable<Translation>>(StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, IEnumerable<Translation>> Translations { get; }
}
