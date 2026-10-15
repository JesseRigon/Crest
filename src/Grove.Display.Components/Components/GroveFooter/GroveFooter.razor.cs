using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives.Rendering;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// GroveFooter component.
    /// </summary>
    // Named App* so the app-frame hosts do not collide with the themes' Layout component or the zone keys.
    [Grove.Display.Management.Components.BlazorComponent("AppFooter", DisplayName = "App footer", Category = "Layout", Icon = "call_to_action")]
    public partial class GroveFooter : GroveComponentWithChildren
    {
        /// <summary>
        /// The <see cref="GroveLayout" /> this component is nested in.
        /// </summary>
        [CascadingParameter]
        public GroveLayout? Layout { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return ClassList.Create("rz-footer")
                            .Add("fixed", Layout == null)
                            .ToString();
        }
    }
}
