using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// GroveContent component.
    /// </summary>
    public partial class GroveContent : GroveComponentWithChildren
    {
        /// <summary>
        /// Gets or sets the container.
        /// </summary>
        /// <value>The container.</value>
        [Parameter]
        public string? Container { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return "content";
        }
    }
}
