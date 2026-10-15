using Grove.Data.Localization.Translations.Models;

namespace Grove.Data.Localization.Translations.ViewModels;

public class TranslationsViewModel
{
    public string Key { get; set; }

    public IEnumerable<Translation> Translations { get; set; } = [];
}
