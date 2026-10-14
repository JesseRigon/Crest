using Crest.ContentLocalization.Drivers;
using Crest.Routing;

namespace Crest.ContentLocalization;

/// <summary>
/// The content culture settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("contentrequestcultureprovider", "Content Culture", "settings-localization", ContentRequestCultureProviderSettingsDriver.GroupId, [ContentLocalizationPermissions.ManageContentCulturePicker.Name]);
        yield return PageEntries.LegacySettings("contentculturepicker", "Content Culture Picker", "settings-localization", ContentCulturePickerSettingsDriver.GroupId, [ContentLocalizationPermissions.ManageContentCulturePicker.Name]);
    }
}
