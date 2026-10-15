#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Infrastructure.Settings;

namespace Grove.Infrastructure.Shells.Admin;

/// <summary>Tenant-scoped Grove title-bar preferences stored alongside Grove site settings.</summary>
public sealed class TitleBarSettingsStore(ISiteService siteService)
{
    private const string SettingsKey = "GroveTitleBar";

    public async ValueTask<TitleBarSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        return TitleBarSettings.Normalize(site.Properties[SettingsKey]?.Deserialize<TitleBarSettings>(JsonSerializerOptions.Web));
    }

    public async ValueTask<TitleBarSettings> SaveAsync(TitleBarSettings? settings, CancellationToken cancellationToken = default)
    {
        var normalized = TitleBarSettings.Normalize(settings);
        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties[SettingsKey] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);
        return normalized;
    }
}

public sealed class TitleBarSettings
{
    public bool DisplayCultureLabel { get; set; }
    public string? TenantAvatarImageUrl { get; set; }
    public string TenantAvatarShape { get; set; } = "Circle";
    public string? TenantAvatarClipPath { get; set; }
    public string? TenantAvatarBorderRadius { get; set; }

    public static TitleBarSettings Normalize(TitleBarSettings? value)
    {
        value ??= new TitleBarSettings();
        var shape = value.TenantAvatarShape?.Trim();
        if (!new[] { "Circle", "RoundedSquare", "Square", "Custom" }.Contains(shape, StringComparer.OrdinalIgnoreCase))
        {
            shape = "Circle";
        }

        return new TitleBarSettings
        {
            DisplayCultureLabel = value.DisplayCultureLabel,
            TenantAvatarImageUrl = NormalizeOptional(value.TenantAvatarImageUrl),
            TenantAvatarShape = shape,
            TenantAvatarClipPath = NormalizeOptional(value.TenantAvatarClipPath),
            TenantAvatarBorderRadius = NormalizeOptional(value.TenantAvatarBorderRadius),
        };
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
