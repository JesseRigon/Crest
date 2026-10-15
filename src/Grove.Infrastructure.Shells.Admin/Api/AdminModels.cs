#nullable enable

using Grove.Infrastructure.Shells.Admin.Models;

namespace Grove.Infrastructure.Shells.Admin.Api;

public sealed record AdminSettingsDto(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar)
{
    public static AdminSettingsDto From(AdminSettings settings) => new(
        settings.DisplayThemeToggler,
        settings.DisplayMenuFilter,
        settings.DisplayNewMenu,
        settings.DisplayTitlesInTopbar);
}

public sealed record TitleBarSettingsDto(
    bool DisplayCultureLabel,
    string? TenantAvatarImageUrl,
    string TenantAvatarShape,
    string? TenantAvatarClipPath,
    string? TenantAvatarBorderRadius)
{
    public static TitleBarSettingsDto From(TitleBarSettings settings) => new(
        settings.DisplayCultureLabel,
        settings.TenantAvatarImageUrl,
        settings.TenantAvatarShape,
        settings.TenantAvatarClipPath,
        settings.TenantAvatarBorderRadius);
}

public sealed record AdminSettingsUpdate(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar);

public sealed record TitleBarSettingsUpdate(
    bool DisplayCultureLabel,
    string? TenantAvatarImageUrl,
    string TenantAvatarShape,
    string? TenantAvatarClipPath,
    string? TenantAvatarBorderRadius);

public sealed record ThemeSettings(string RadzenTheme, Dictionary<string, string> Tokens)
{
    public static ThemeSettings Default { get; } = new(
        "material-base",
        new Dictionary<string, string>
        {
            ["primary"] = "#2f6f4e",
            ["secondary"] = "#6d5d3f",
            ["surface"] = "#ffffff",
            ["background"] = "#f7f8f6",
            ["radius"] = "6px",
        });
}
