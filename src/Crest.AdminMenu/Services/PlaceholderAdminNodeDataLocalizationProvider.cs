using Crest.AdminMenu.AdminNodes;
using Crest.Localization.Data;

namespace Crest.AdminMenu.Services;

public class PlaceholderAdminNodeDataLocalizationProvider : AdminNodeDataLocalizationProvider
{
    public PlaceholderAdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuRetrieval) : base(adminMenuRetrieval)
    {
    }

    public override async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var adminMenuList = await GetAdminMenusAsync();

        return adminMenuList.SelectMany(m => Descriptors(
            DataLocalizationContext.AdminMenu(m.Name),
            AllNodes(m.MenuItems).OfType<PlaceholderAdminNode>().Select(n => n.LinkText)));
    }
}
