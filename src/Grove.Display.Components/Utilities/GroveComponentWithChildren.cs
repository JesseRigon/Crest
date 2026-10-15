using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// A base class of components that have child content.
/// </summary>
public class GroveComponentWithChildren : GroveComponent
{
    /// <summary>
    /// Gets or sets the child content
    /// </summary>
    /// <value>The content of the child.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}

