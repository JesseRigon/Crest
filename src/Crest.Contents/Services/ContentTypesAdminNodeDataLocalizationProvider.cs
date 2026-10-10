using Crest.AdminMenu;
using Crest.AdminMenu.Services;
using Crest.Contents.AdminNodes;
using Crest.Localization.Data;

namespace Crest.Contents.Services;

public class ContentTypesAdminNodeDataLocalizationProvider : AdminNodeDataLocalizationProvider
{
    public ContentTypesAdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuRetrieval) : base(adminMenuRetrieval)
    {
    }

    public override async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var adminMenuList = await GetAdminMenusAsync();

        return adminMenuList.SelectMany(m => Descriptors(
            Crest.AdminMenu.DataLocalizationContext.AdminMenu(m.Name),
            AllNodes(m.MenuItems).OfType<ContentTypesAdminNode>()
                .SelectMany(n => n.ContentTypes)
                .Select(e => e.ContentTypeDisplayName)));
    }
}
