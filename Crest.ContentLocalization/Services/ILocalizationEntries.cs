using Crest.ContentLocalization.Models;

namespace Crest.ContentLocalization.Services;

public interface ILocalizationEntries
{
    Task<(bool, LocalizationEntry)> TryGetLocalizationAsync(string contentItemId);
    Task<IEnumerable<LocalizationEntry>> GetLocalizationsAsync(string localizationSet);
    Task UpdateEntriesAsync();
}
