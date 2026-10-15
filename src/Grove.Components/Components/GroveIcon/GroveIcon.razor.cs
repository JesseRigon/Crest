using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Components.Display;

/// <summary>
/// An icon from the icon registry, by key: a registered primitive (docs/blazor-display.md,
/// rework step 1), its parameters declared here so the catalog generator reads them.
/// </summary>
[BlazorComponent("Icon", DisplayName = "Icon", Category = "Basic", Icon = "emoji_symbols")]
public partial class GroveIcon : ComponentBase
{
    [Parameter]
    [ComponentParameter(DisplayName = "Icon", Group = "Content", Required = true)]
    public string? IconKey { get; set; }

    /// <summary>A length from the design system (<c>1.5rem</c>, a size token).</summary>
    [Parameter]
    public string? Size { get; set; }

    [Parameter]
    [ComponentParameter(Editor = "color")]
    public string? Color { get; set; }

    /// <summary>The accessible name; without one the icon is decorative.</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Alt { get; set; }

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Style { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
