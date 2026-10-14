using Crest.AzureAI.Drivers;
using Crest.AzureAI.Models;
using Crest.Indexing;
using Crest.Routing;
using Microsoft.Extensions.Options;

namespace Crest.AzureAI;

/// <summary>
/// The Azure AI Search settings, unless the host configures them: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages(IOptionsMonitor<AzureAISearchDefaultOptions> azureAISearchSettings) : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        if (azureAISearchSettings.CurrentValue.DisableUIConfiguration)
        {
            yield break;
        }

        yield return PageEntries.LegacySettings("azureaisearch", "Azure AI Search", "settings-search", AzureAISearchDefaultSettingsDisplayDriver.GroupId, [IndexingPermissions.ManageIndexes.Name]);
    }
}
