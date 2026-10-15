using Grove.DataLocalization.Models;

namespace Grove.DataLocalization.ViewModels;

public class TranslationsViewModel
{
    public string Key { get; set; }

    public IEnumerable<Translation> Translations { get; set; } = [];
}
