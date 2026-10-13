using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives;

/// <summary>
/// CrestQuote component - renders a single block quote; content-driven,
/// placed by templates, page trees and layer rules.
/// </summary>
[BlazorComponent("CrestQuote", DisplayName = "Quote", Category = "Typography", Icon = "format_quote")]
public partial class CrestQuote : ComponentBase
{
    [Parameter]
    [ComponentParameter(DisplayName = "Quote", Group = "Content", Required = true)]
    public string? Text { get; set; }
}
