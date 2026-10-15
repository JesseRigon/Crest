using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// GroveDropZoneContainer component.
    /// </summary>
    [CascadingTypeParameter(nameof(TItem))]
    public partial class GroveDropZoneContainer<TItem> : GroveComponentWithChildren
    {
        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        /// <value>The data.</value>
        [Parameter]
        public IEnumerable<TItem>? Data { get; set; }

        /// <summary>
        /// Gets or sets the selector function for zone items.
        /// </summary>
        /// <value>The selector function for zone items.</value>
        [Parameter]
        public Func<TItem, GroveDropZone<TItem>, bool>? ItemSelector { get; set; }

        /// <summary>
        /// Gets or sets the function that checks if the item can be dropped in specific zone or item.
        /// </summary>
        /// <value>The function that checks if the item can be dropped in specific zone.</value>
        [Parameter]
        public Func<GroveDropZoneItemEventArgs<TItem>, bool>? CanDrop { get; set; }

        /// <summary>
        /// Gets or sets the row render callback. Use it to set row attributes.
        /// </summary>
        /// <value>The row render callback.</value>
        [Parameter]
        public Action<GroveDropZoneItemRenderEventArgs<TItem>>? ItemRender { get; set; }

        /// <summary>
        /// Gets or sets the template for zone items.
        /// </summary>
        /// <value>The template for zone items.</value>
        [Parameter]
        public RenderFragment<TItem>? Template { get; set; }

        /// <summary>
        /// The event callback raised on item drop.
        /// </summary>
        /// <value>The event callback raised on item drop.</value>
        [Parameter]
        public EventCallback<GroveDropZoneItemEventArgs<TItem>> Drop { get; set; }

        /// <summary>
        /// The event callback raised when an item drag starts.
        /// </summary>
        /// <value>The event callback raised when an item drag starts.</value>
        [Parameter]
        public EventCallback<GroveDropZoneItemEventArgs<TItem>> DragStart { get; set; }

        /// <summary>
        /// The event callback raised when an item drag ends.
        /// </summary>
        /// <value>The event callback raised when an item drag ends.</value>
        [Parameter]
        public EventCallback<GroveDropZoneItemEventArgs<TItem>> DragEnd { get; set; }

        internal GroveDropZoneItemEventArgs<TItem>? Payload { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return "rz-dropzone-container";
        }
    }
}