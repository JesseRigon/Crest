#nullable enable

using System.Text.Json.Nodes;
using Grove.Environment.Shell.Scope;
using Grove.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.AuditTrail.Recorders;

/// <summary>
/// Records which settings sections a write changed (docs/audit.md › step 3: settings and
/// themes). Every settings screen, the theme services and recipes write through
/// <see cref="ISiteService.UpdateSiteSettingsAsync"/>, so one decorator covers them. Only the
/// names of the changed sections are stored, never their values (settings hold secrets); a theme
/// change is its own event with the theme names.
/// </summary>
public sealed class AuditingSiteService(ISiteService inner) : ISiteService
{
    private const string SiteThemeKey = "CurrentThemeName";
    private const string AdminThemeKey = "CurrentAdminThemeName";

    public Task<ISite> LoadSiteSettingsAsync() => inner.LoadSiteSettingsAsync();

    public Task<ISite> GetSiteSettingsAsync() => inner.GetSiteSettingsAsync();

    public async Task UpdateSiteSettingsAsync(ISite site)
    {
        JsonObject? before = null;
        var recorder = ShellScope.Current?.ServiceProvider.GetService<TenantAuditTrailRecorder>();
        if (recorder is not null)
        {
            before = JObject.FromObject(await inner.GetSiteSettingsAsync());
        }

        await inner.UpdateSiteSettingsAsync(site);

        if (recorder is null || before is null || JObject.FromObject(site) is not { } after)
        {
            return;
        }

        var sections = ChangedSections(before, after).ToList();
        if (sections.Remove(SiteThemeKey))
        {
            await RecordThemeAsync(recorder, TenantAuditTrailEventConfiguration.SiteThemeChanged, before, after, SiteThemeKey);
        }

        if (sections.Remove(AdminThemeKey))
        {
            await RecordThemeAsync(recorder, TenantAuditTrailEventConfiguration.AdminThemeChanged, before, after, AdminThemeKey);
        }

        if (sections.Count > 0)
        {
            await recorder.RecordAsync(
                TenantAuditTrailEventConfiguration.Settings,
                TenantAuditTrailEventConfiguration.SettingsUpdated,
                "Site",
                new TenantAuditTrailEvent { Sections = sections },
                "Settings",
                string.Join(", ", sections));
        }
    }

    /// <summary>The top-level settings that differ (as "General") and each section in the properties bag that differs.</summary>
    public static IEnumerable<string> ChangedSections(JsonObject before, JsonObject after)
    {
        var general = before.Where(pair => pair.Key != nameof(ISite.Properties)).Select(pair => pair.Key)
            .Union(after.Where(pair => pair.Key != nameof(ISite.Properties)).Select(pair => pair.Key))
            .Any(key => !JsonNode.DeepEquals(before[key], after[key]));
        if (general)
        {
            yield return "General";
        }

        var beforeProperties = before[nameof(ISite.Properties)] as JsonObject ?? [];
        var afterProperties = after[nameof(ISite.Properties)] as JsonObject ?? [];
        foreach (var key in beforeProperties.Select(pair => pair.Key).Union(afterProperties.Select(pair => pair.Key)).Order(StringComparer.Ordinal))
        {
            if (!JsonNode.DeepEquals(beforeProperties[key], afterProperties[key]))
            {
                yield return key;
            }
        }
    }

    private static Task RecordThemeAsync(TenantAuditTrailRecorder recorder, string name, JsonObject before, JsonObject after, string key)
    {
        var previous = (before[nameof(ISite.Properties)] as JsonObject)?[key]?.ToString();
        var current = (after[nameof(ISite.Properties)] as JsonObject)?[key]?.ToString();
        return recorder.RecordAsync(
            TenantAuditTrailEventConfiguration.Themes,
            name,
            current ?? key,
            new TenantAuditTrailEvent { Name = current, Previous = previous, Current = current },
            "Theme",
            current);
    }
}
