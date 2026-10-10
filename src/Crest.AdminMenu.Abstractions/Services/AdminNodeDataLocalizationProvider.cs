using Crest.Localization.Data;
using Crest.Navigation;

namespace Crest.AdminMenu.Services;

public abstract class AdminNodeDataLocalizationProvider : ILocalizationDataProvider
{
    private readonly IAdminMenuAccessor _adminMenuRetrieval;

    public AdminNodeDataLocalizationProvider(IAdminMenuAccessor adminMenuRetrieval)
    {
        _adminMenuRetrieval = adminMenuRetrieval;
    }

    public abstract Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync();

    protected async Task<IEnumerable<Models.AdminMenu>> GetAdminMenusAsync()
        => await _adminMenuRetrieval.GetAdminMenusAsync();

    /// <summary>
    /// Every node of a menu, roots and descendants alike, so a caption anywhere in the tree is
    /// enumerated for the Translations editor.
    /// </summary>
    protected static IEnumerable<MenuItem> AllNodes(IEnumerable<MenuItem> items)
    {
        foreach (var item in items)
        {
            yield return item;

            foreach (var child in AllNodes(item.Items))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// One descriptor per distinct caption under a menu's context: a caption two nodes share is
    /// one entry, so the editor shows one row and its save stores one value.
    /// </summary>
    protected static IEnumerable<DataLocalizedString> Descriptors(string context, IEnumerable<string> captions)
        => captions
            .Where(caption => !string.IsNullOrWhiteSpace(caption))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(caption => new DataLocalizedString(context, caption, string.Empty));
}
