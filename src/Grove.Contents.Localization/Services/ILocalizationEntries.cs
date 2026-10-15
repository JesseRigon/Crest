using Grove.Contents.Localization.Models;

namespace Grove.Contents.Localization.Services;

public interface ILocalizationEntries
{
    Task<(bool, LocalizationEntry)> TryGetLocalizationAsync(string contentItemId);
    Task<IEnumerable<LocalizationEntry>> GetLocalizationsAsync(string localizationSet);
    Task UpdateEntriesAsync();
}
