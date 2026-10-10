using Crest.DataLocalization.Models;

namespace Crest.DataLocalization.ViewModels;

public class TranslationsViewModel
{
    public string Key { get; set; }

    public IEnumerable<Translation> Translations { get; set; } = [];
}
