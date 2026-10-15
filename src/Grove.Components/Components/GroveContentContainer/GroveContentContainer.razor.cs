using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// GroveContentContainer component.
    /// </summary>
    public partial class GroveContentContainer : GroveComponentWithChildren
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [Parameter]
        public string? Name { get; set; }
    }
}
