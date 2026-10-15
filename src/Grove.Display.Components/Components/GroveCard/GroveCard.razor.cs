using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives.Rendering;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// A card container component that groups related content with a consistent visual design and optional elevation.
    /// GroveCard provides a versatile styled container for displaying information, images, actions, and other content in a structured format.
    /// Supports different visual variants (Filled, Flat, Outlined, Text) that affect the card's appearance.
    /// Works well in grid layouts (using GroveRow/GroveColumn) or can be stacked vertically.
    /// Ideal for grouping related information, creating dashboard widgets, displaying product information, or organizing form sections.
    /// Combine with other Grove components like GroveImage, GroveText, and GroveButton for rich card content.
    /// </summary>
    /// <example>
    /// Basic card with content:
    /// <code>
    /// &lt;GroveCard&gt;
    ///     &lt;GroveText TextStyle="TextStyle.H6"&gt;Card Title&lt;/GroveText&gt;
    ///     &lt;GroveText&gt;Card content goes here...&lt;/GroveText&gt;
    /// &lt;/GroveCard&gt;
    /// </code>
    /// Card with custom variant:
    /// <code>
    /// &lt;GroveCard Variant="Variant.Outlined" Style="padding: 2rem;"&gt;
    ///     &lt;GroveImage Path="product.jpg" Style="width: 100%; height: 200px; object-fit: cover;" /&gt;
    ///     &lt;GroveText TextStyle="TextStyle.H5"&gt;Product Name&lt;/GroveText&gt;
    ///     &lt;GroveText&gt;Product description...&lt;/GroveText&gt;
    ///     &lt;GroveButton Text="Buy Now" ButtonStyle="ButtonStyle.Primary" /&gt;
    /// &lt;/GroveCard&gt;
    /// </code>
    /// </example>
    [Grove.Display.Management.Components.BlazorComponent("Card", DisplayName = "Card", Category = "Layout", Icon = "crop_portrait")]
    public partial class GroveCard : GroveComponentWithChildren
    {
        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-card")
                                                                     .AddVariant(Variant)
                                                                     .ToString();

        /// <summary>
        /// Gets or sets the visual design variant of the card.
        /// Controls the card's appearance: Filled (solid background with elevation), Flat (subtle background), 
        /// Outlined (border only), or Text (minimal styling).
        /// </summary>
        /// <value>The card variant. Default is <see cref="Variant.Filled"/>.</value>
        [Parameter]
        public Variant Variant { get; set; } = Variant.Filled;
    }
}