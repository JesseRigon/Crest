using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives.Rendering;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// GroveHeader component.
    /// </summary>
    // Named App* so the app-frame hosts do not collide with the themes' Layout component or the zone keys.
    [Grove.Display.Management.Components.BlazorComponent("AppHeader", DisplayName = "App header", Category = "Layout", Icon = "web_asset")]
    public partial class GroveHeader : GroveComponentWithChildren
    {
        /// <summary>
        /// The <see cref="GroveLayout" /> this component is nested in.
        /// </summary>
        [CascadingParameter]
        public GroveLayout? Layout { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return ClassList.Create("rz-header")
                            .Add("fixed", Layout == null)
                            .ToString();
        }
    }
}
