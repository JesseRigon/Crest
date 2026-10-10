using Crest.AdminMenu.AdminNodes;
using Crest.Localization.Data;

namespace Crest.AdminMenu.Services;

public class LinkAdminNodeDataLocalizationProvider : AdminNodeDataLocalizationProvider
{
    public LinkAdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuAccessor) : base(adminMenuAccessor)
    {
    }

    public override async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var adminMenuList = await GetAdminMenusAsync();

        return adminMenuList.SelectMany(m => Descriptors(
            DataLocalizationContext.AdminMenu(m.Name),
            AllNodes(m.MenuItems).OfType<LinkAdminNode>().Select(n => n.LinkText)));
    }
}
