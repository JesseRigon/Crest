using Grove.Data.Localization.Data;
using Grove.Routing;

namespace Grove.Services;

/// <summary>
/// The admin menu's declared captions for the Translations editor, under the menu's translation
/// context ("Admin Menus:Primary Navigation"): every caption of the page registry's declared
/// admin entries except data captions (content type and kind names translate under their own
/// contexts).
/// </summary>
public sealed class PageMenuLocalizationDataProvider(IPageRegistry registry) : ILocalizationDataProvider
{
    public async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var context = Grove.Contents.AdminMenu.DataLocalizationContext.AdminMenu(AdminMenuBuilder.TranslationMenuName);
        return (await registry.GetDeclaredAsync()).InShell(ShellId.Admin)
            .Where(entry => entry.Menu is { CaptionIsData: false })
            .Select(entry => entry.Menu!.Caption)
            .Where(caption => !string.IsNullOrWhiteSpace(caption))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(caption => new DataLocalizedString(context, caption, string.Empty))
            .ToArray();
    }
}
