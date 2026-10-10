using Crest.AdminMenu;
using Crest.AdminMenu.Services;
using Crest.Localization.Data;

namespace Crest.DataLocalization.Services;

/// <summary>
/// Keeps the translation store in step with the admin menus: a saved menu drops the entries
/// under its context whose caption no provider enumerates any more (a deleted or renamed
/// node), and a deleted menu takes its whole context with it, plus the menu name's own entry
/// in the generic admin-menus context when no other menu still bears the name.
/// </summary>
public sealed class AdminMenuTranslationCleanup(
    ITranslationsManager translationsManager,
    IAdminMenuAccessor adminMenus,
    IEnumerable<ILocalizationDataProvider> providers) : IAdminMenuEventHandler
{
    public async Task SavedAsync(Crest.AdminMenu.Models.AdminMenu menu)
    {
        var context = DataLocalizationContext.AdminMenu(menu.Name);

        // The live captions are what the editor would show: every provider's descriptors under
        // this menu's context, the same enumeration the Translations editor rows come from.
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            foreach (var descriptor in await provider.GetDescriptorsAsync())
            {
                if (string.Equals(descriptor.Context, context, StringComparison.OrdinalIgnoreCase))
                {
                    live.Add(descriptor.Name);
                }
            }
        }

        var document = await translationsManager.GetTranslationsDocumentAsync();
        var orphaned = document.Translations.Values
            .SelectMany(entries => entries)
            .Where(entry => string.Equals(entry.Context, context, StringComparison.OrdinalIgnoreCase) && !live.Contains(entry.Key))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await translationsManager.RemoveTranslationsAsync(context, orphaned);
    }

    public async Task RemovedAsync(Crest.AdminMenu.Models.AdminMenu menu)
    {
        await translationsManager.RemoveContextAsync(DataLocalizationContext.AdminMenu(menu.Name));

        // Menu names are not enforced unique: the name's own entry goes only with the last menu
        // that bears it.
        var nameStillUsed = (await adminMenus.GetAdminMenusAsync()).Any(other =>
            !string.Equals(other.Id, menu.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(other.Name, menu.Name, StringComparison.OrdinalIgnoreCase));

        if (!nameStillUsed)
        {
            await translationsManager.RemoveTranslationsAsync(DataLocalizationContext.AdminMenu(), [menu.Name]);
        }
    }
}
