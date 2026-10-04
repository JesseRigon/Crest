using Crest.Themes;

namespace Crest.ViewModels;

/// <summary>The theme-selection state: one section per shell, and every selectable theme.</summary>
/// <param name="Shells">
/// Site, Admin and - only when an enabled feature ships member pages - Member, in that order.
/// </param>
public sealed record ThemesState(ThemeShell[] Shells, ThemeSummary[] Themes);

/// <summary>One shell's section: its bucket and its active theme.</summary>
/// <param name="Shell">A <c>CrestShells</c> value: "site", "admin" or "member".</param>
/// <param name="CanReset">
/// Whether the shell can be left with no theme. The member shell cannot: Crest's member
/// theme is its default rather than "none".
/// </param>
public sealed record ThemeShell(string Shell, string? CurrentThemeId, bool CanReset);

public sealed record ThemeSummary(
    string Id,
    string Name,
    string Description,
    string Author,
    string Website,
    string Version,
    string ExtensionId,
    string Shell,
    bool IsCrestBlazor,
    string[] BaseThemes,
    bool IsCurrent,
    bool Enabled,
    string PreviewImageUrl,
    // For the current theme: the enabled features it fails now. For any other theme: the
    // enabled features that would fail if it became current.
    ShellIncompatibility[] Incompatibilities);
