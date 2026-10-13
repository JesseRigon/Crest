using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Settings;

namespace Crest.Services;

public sealed class PrimaryNavMenuSettingsStore(ISiteService siteService)
{
    private const string SettingsKey = "CrestAdminPrimaryNavMenu";
    private const string PreviousSettingsKey = "CrestAdminSidebar";

    public async ValueTask<PrimaryNavMenuSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        if (site.Properties[SettingsKey] is null && site.Properties.TryGetPropertyValue(PreviousSettingsKey, out var previous))
        {
            // Move the tenant document to its new contract once. Runtime paths
            // thereafter use only the primary-navigation key and shape.
            site.Properties[SettingsKey] = previous?.DeepClone();
            site.Properties.Remove(PreviousSettingsKey);
            await siteService.UpdateSiteSettingsAsync(site);
        }

        return PrimaryNavMenuSettings.Normalize(
            site.Properties[SettingsKey]?.Deserialize<PrimaryNavMenuSettings>(JsonSerializerOptions.Web));
    }

    public async ValueTask<PrimaryNavMenuSettings> SaveAsync(PrimaryNavMenuSettings? settings, CancellationToken cancellationToken = default)
    {
        var normalized = PrimaryNavMenuSettings.Normalize(settings);
        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties[SettingsKey] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);
        return normalized;
    }
}
