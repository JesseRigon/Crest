using Grove.Data.Documents;

namespace Grove.Data.Localization.Translations.Models;

public class TranslationsDocument : Document
{
    public TranslationsDocument()
    {
        Translations = new Dictionary<string, IEnumerable<Translation>>(StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, IEnumerable<Translation>> Translations { get; }
}
