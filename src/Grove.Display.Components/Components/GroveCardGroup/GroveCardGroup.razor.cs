using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives.Rendering;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// GroveCardGroup component.
    /// </summary>
    public partial class GroveCardGroup : GroveComponentWithChildren
    {
        /// <summary>
        /// Toggles the responsive mode of the component. If set to <c>true</c> (the default) the component will be 
        /// expanded on larger displays and collapsed on touch devices. Set to <c>false</c> if you want to disable this behavior.
        /// </summary>
        [Parameter]
        public bool Responsive { get; set; } = true;
        
        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-card-group")
                                                                      .Add("rz-card-group-responsive", Responsive)
                                                                      .ToString();
    }
}