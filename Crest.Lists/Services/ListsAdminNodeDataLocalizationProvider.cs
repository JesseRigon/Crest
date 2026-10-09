using Crest.AdminMenu;
using Crest.AdminMenu.Services;
using Crest.Lists.AdminNodes;
using Crest.Localization.Data;

namespace Crest.Lists.Services;

public class ListsAdminNodeDataLocalizationProvider : AdminNodeDataLocalizationProvider
{
    public ListsAdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuAccessor) : base(adminMenuAccessor)
    {
    }

    public override async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var adminMenuList = await GetAdminMenusAsync();

        return adminMenuList.SelectMany(m =>
        {
            var context = DataLocalizationContext.AdminMenu(m.Name);

            return m.MenuItems.OfType<ListsAdminNode>()
                .Select(n => new DataLocalizedString(context, n.ContentType, string.Empty));
        });
    }
}
