using Grove.ContentLocalization.Models;

namespace Grove.ContentLocalization.Services;

public interface ILocalizationEntries
{
    Task<(bool, LocalizationEntry)> TryGetLocalizationAsync(string contentItemId);
    Task<IEnumerable<LocalizationEntry>> GetLocalizationsAsync(string localizationSet);
    Task UpdateEntriesAsync();
}
