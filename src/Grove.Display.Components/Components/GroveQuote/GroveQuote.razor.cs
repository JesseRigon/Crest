using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// GroveQuote component - renders a single block quote; content-driven,
/// placed by templates, page trees and layer rules.
/// </summary>
[BlazorComponent("GroveQuote", DisplayName = "Quote", Category = "Typography", Icon = "format_quote")]
public partial class GroveQuote : ComponentBase
{
    [Parameter]
    [ComponentParameter(DisplayName = "Quote", Group = "Content", Required = true)]
    public string? Text { get; set; }
}
