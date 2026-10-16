using Grove.Contents.Localization.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Data.Localization;
using Grove.Entities;

namespace Grove.Contents.Localization.Services;

/// <summary>
/// What the localization's system flows do (<see cref="Workflows.LocalizationWorkflowProvider"/>): a
/// new item gets a localization set and the default culture; a clone gets a set of its own; publishing,
/// unpublishing and removing keep the localization entries.
/// </summary>
public sealed class LocalizationBehaviour(
    ILocalizationEntries entries,
    IIdGenerator idGenerator,
    ILocalizationService localizationService)
{
    /// <summary>A new item's localization set and culture, when it has none.</summary>
    public async Task SetCultureAndSetAsync(ContentItem item)
    {
        // The flow runs only where the field set is attached: an item that does not hold it yet gets it.
        var part = item.GetOrCreate<LocalizationFieldSet>();
        if (string.IsNullOrEmpty(part.LocalizationSet))
        {
            item.Alter<LocalizationFieldSet>(localization => localization.LocalizationSet = idGenerator.GenerateUniqueId());
        }

        if (string.IsNullOrEmpty(part.Culture))
        {
            var culture = await localizationService.GetDefaultCultureAsync();
            item.Alter<LocalizationFieldSet>(localization => localization.Culture = culture);
        }
    }

    /// <summary>A clone is no translation of its source: its set is its own.</summary>
    public Task ClearSetAsync(ContentItem clone)
    {
        if (clone.Has<LocalizationFieldSet>())
        {
            clone.Alter<LocalizationFieldSet>(localization => localization.LocalizationSet = string.Empty);
        }

        return Task.CompletedTask;
    }

    /// <summary>The localization entries, updated from the index once the unit commits.</summary>
    public Task RegisterEntriesAsync(ContentItem item) =>
        item.TryGet<LocalizationFieldSet>(out var part) && !string.IsNullOrWhiteSpace(part.LocalizationSet) && part.Culture != null
            ? entries.UpdateEntriesAsync()
            : Task.CompletedTask;
}
