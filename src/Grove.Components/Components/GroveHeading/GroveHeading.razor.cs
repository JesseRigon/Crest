using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// GroveHeading component.
    /// </summary>
    [BlazorComponent("GroveHeading", DisplayName = "Heading", Category = "Typography", Icon = "title")]
    public partial class GroveHeading : GroveComponent
    {
        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>The text.</value>
        [Parameter]
        [ComponentParameter(DisplayName = "Text", Group = "Content", Required = true)]
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        /// <value>The size.</value>
        [Parameter]
        [ComponentParameter(DisplayName = "Level", Group = "Content", Choices = ["H1", "H2", "H3", "H4", "H5", "H6"])]
        public string Size { get; set; } = "H1";

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return "rz-heading";
        }
    }
}
