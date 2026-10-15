using Microsoft.AspNetCore.Components;
using System.Collections.Generic;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A breadcrumb navigation component that displays the current page's location within the application hierarchy.
    /// GroveBreadCrumb shows a trail of links representing the path from the root to the current page, helping users understand their location and navigate back.
    /// Provides secondary navigation with items separated by a visual divider (typically ">"), with each item linking to its respective page.
    /// Common uses include multi-level navigation indicating current location, e-commerce category navigation (Home > Electronics > Laptops), documentation section paths, and file system or folder navigation.
    /// Items are defined using GroveBreadCrumbItem components as child content.
    /// The last item typically represents the current page and is often not clickable.
    /// </summary>
    /// <example>
    /// Basic breadcrumb:
    /// <code>
    /// &lt;GroveBreadCrumb&gt;
    ///     &lt;GroveBreadCrumbItem Text="Home" Path="/" /&gt;
    ///     &lt;GroveBreadCrumbItem Text="Products" Path="/products" /&gt;
    ///     &lt;GroveBreadCrumbItem Text="Laptops" /&gt;
    /// &lt;/GroveBreadCrumb&gt;
    /// </code>
    /// Breadcrumb with icons and custom template:
    /// <code>
    /// &lt;GroveBreadCrumb&gt;
    ///     &lt;GroveBreadCrumbItem Text="Home" Path="/" Icon="home" /&gt;
    ///     &lt;GroveBreadCrumbItem Text="Settings" Path="/settings" Icon="settings" /&gt;
    ///     &lt;GroveBreadCrumbItem Text="Profile" Icon="person" /&gt;
    /// &lt;/GroveBreadCrumb&gt;
    /// </code>
    /// </example>
    public partial class GroveBreadCrumb : GroveComponentWithChildren
    {
        /// <summary>
        /// Gets or sets a custom template for rendering breadcrumb items.
        /// When set, this template is used instead of the default rendering for each item, allowing complete control over item appearance.
        /// The template receives a GroveBreadCrumbItem as context.
        /// </summary>
        /// <value>The custom item template render fragment.</value>
        [Parameter]
        public RenderFragment<GroveBreadCrumbItem>? Template { get; set; }

        /// <summary>
        /// Gets or sets the accessible label for the breadcrumb navigation landmark.
        /// Rendered as the <c>aria-label</c> attribute on the root <c>&lt;nav&gt;</c> element so assistive technology can distinguish this landmark from other navigation regions.
        /// </summary>
        /// <value>The navigation landmark label. Defaults to <c>"breadcrumb"</c>.</value>
        [Parameter]
        public string AriaLabel { get => ariaLabel ?? Localize(nameof(GroveStrings.BreadCrumb_AriaLabel)); set => ariaLabel = value; }

        private string? ariaLabel;

        /// <inheritdoc/>
        protected override string GetComponentCssClass()
        {
            return "rz-breadcrumb";
        }

        readonly List<GroveBreadCrumbItem> items = new List<GroveBreadCrumbItem>();

        internal void AddItem(GroveBreadCrumbItem item)
        {
            if (!items.Contains(item))
            {
                var previousLast = items.Count > 0 ? items[items.Count - 1] : null;

                items.Add(item);

                previousLast?.Refresh();
            }
        }

        internal void RemoveItem(GroveBreadCrumbItem item)
        {
            if (items.Remove(item) && !disposed && items.Count > 0)
            {
                try
                {
                    items[items.Count - 1].Refresh();
                }
                catch
                {
                }
            }
        }

        internal bool IsLastItem(GroveBreadCrumbItem item)
        {
            return items.Count > 0 && items[items.Count - 1] == item;
        }
    }

}
