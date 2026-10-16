using Grove.Contents.AdminMenu;
using Grove.Contents.AdminMenu.Services;
using Grove.Contents.Hierarchy.AdminNodes;
using Grove.Data.Localization.Data;

namespace Grove.Contents.Hierarchy.Services;

public class ListsAdminNodeDataLocalizationProvider : AdminNodeDataLocalizationProvider
{
    public ListsAdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuAccessor) : base(adminMenuAccessor)
    {
    }

    public override async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var adminMenuList = await GetAdminMenusAsync();

        return adminMenuList.SelectMany(m => Descriptors(
            DataLocalizationContext.AdminMenu(m.Name),
            AllNodes(m.MenuItems).OfType<ListsAdminNode>().Select(n => n.ContentType)));
    }
}
