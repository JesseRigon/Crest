using Grove.AdminMenu;
using Grove.AdminMenu.Services;
using Grove.Contents.AdminNodes;
using Grove.Localization.Data;

namespace Grove.Contents.Services;

public class ContentTypesAdminNodeDataLocalizationProvider : AdminNodeDataLocalizationProvider
{
    public ContentTypesAdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuRetrieval) : base(adminMenuRetrieval)
    {
    }

    public override async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var adminMenuList = await GetAdminMenusAsync();

        return adminMenuList.SelectMany(m => Descriptors(
            Grove.AdminMenu.DataLocalizationContext.AdminMenu(m.Name),
            AllNodes(m.MenuItems).OfType<ContentTypesAdminNode>()
                .SelectMany(n => n.ContentTypes)
                .Select(e => e.ContentTypeDisplayName)));
    }
}
