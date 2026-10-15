using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using Grove.AdminTheme.Api;
using Grove.Components.Primitives;

namespace Grove.AdminTheme.Theme;

public sealed partial class ThemeEngine(IJSRuntime js)
{
    private static readonly IReadOnlyDictionary<string, string> SemanticTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["primary"] = "--grove-color-accent-1",
        ["secondary"] = "--grove-color-accent-2",
        ["info"] = "--grove-color-accent-3",
        ["onPrimary"] = "--grove-color-on-accent-1",
        ["surface"] = "--grove-color-surface-1",
        ["background"] = "--grove-color-surface-2",
        ["primaryNavMenuSurface"] = "--grove-color-surface-3",
        ["text"] = "--grove-color-text-1",
        ["titleText"] = "--grove-color-text-2",
        ["mutedText"] = "--grove-color-text-muted",
        ["border"] = "--grove-color-border-1",
        ["radius"] = "--grove-radius-sm",
    };

    private static readonly HashSet<string> AllowedGroveVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "--grove-color-accent-1",
        "--grove-color-accent-2",
        "--grove-color-accent-3",
        "--grove-color-on-accent-1",
        "--grove-color-surface-1",
        "--grove-color-surface-2",
        "--grove-color-surface-3",
        "--grove-color-text-1",
        "--grove-color-text-2",
        "--grove-color-text-muted",
        "--grove-color-border-1",
        "--grove-color-shadow-1",
        "--grove-color-hover-surface-1",
        "--grove-color-hover-text-1",
        "--grove-color-active-surface-1",
        "--grove-color-button-hover-surface-1",
        "--grove-border-size-xs",
        "--grove-border-size-sm",
        "--grove-border-size-md",
        "--grove-radius-xs",
        "--grove-radius-sm",
        "--grove-radius-md",
        "--grove-radius-lg",
        "--grove-radius-pill",
        "--grove-space-2xs",
        "--grove-space-xs",
        "--grove-space-sm",
        "--grove-space-md",
        "--grove-space-lg",
        "--grove-space-xl",
        "--grove-space-2xl",
        "--grove-font-size-xs",
        "--grove-font-size-sm",
        "--grove-font-size-md",
        "--grove-font-size-lg",
        "--grove-font-size-xl",
        "--grove-shadow-sm",
        "--grove-shadow-md",
        "--grove-shadow-lg",
    };

    public async Task ApplyAsync(ThemeSettings settings)
    {
        var variables = Translate(settings.Tokens);
        // Prerender-safe: applying theme CSS variables is a browser-only effect; the
        // interactive phase re-runs the caller's init with working interop and
        // reapplies. See PrerenderSafeJs.
        await js.TryInvokeVoidAsync("groveTheme.apply", settings.RadzenTheme, variables);
    }

    private static Dictionary<string, string> Translate(IReadOnlyDictionary<string, string> tokens)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in tokens)
        {
            var variable = SemanticTokens.TryGetValue(key, out var semanticVariable)
                ? semanticVariable
                : key;

            if (!AllowedGroveVariables.Contains(variable) || !IsSafeCssValue(value))
            {
                continue;
            }

            variables[variable] = value;
        }

        return variables;
    }

    private static bool IsSafeCssValue(string value) =>
        value.Length <= 64 && SafeCssValueRegex().IsMatch(value);

    [GeneratedRegex(@"^#[0-9a-fA-F]{3,8}$|^[a-zA-Z][a-zA-Z0-9-]*$|^-?(\d+|\d*\.\d+)(px|rem|em|%)$|^rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*(0|1|0?\.\d+))?\s*\)$")]
    private static partial Regex SafeCssValueRegex();
}
