using Microsoft.AspNetCore.Components;
using Grove.Components.Primitives.Rendering;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A skeleton screen component that displays placeholder shapes while content is loading.
    /// GroveSkeleton provides subtle loading states that match content structure, improving perceived performance.
    /// Shows gray placeholder shapes that mimic the structure of the content being loaded, providing better UX than spinners by showing users the approximate layout before content appears,
    /// making loading feel faster with immediate feedback, and reducing anxiety through progressive disclosure.
    /// Supports multiple shapes including Text (horizontal bars for text lines, default), Circle (circular placeholders for avatars or icons), and Rectangle (rectangular blocks for images or cards).
    /// Animations (None, Pulse, Wave) can be applied for additional loading feedback. Use multiple skeletons to represent the full structure of your loading content.
    /// </summary>
    /// <example>
    /// Text line skeleton:
    /// <code>
    /// &lt;GroveSkeleton Style="width: 100%; height: 20px;" Animation="SkeletonAnimation.Pulse" /&gt;
    /// </code>
    /// Avatar and text skeleton:
    /// <code>
    /// &lt;GroveStack Orientation="Orientation.Horizontal" Gap="1rem" AlignItems="AlignItems.Center"&gt;
    ///     &lt;GroveSkeleton Variant="SkeletonVariant.Circle" Style="width: 50px; height: 50px;" /&gt;
    ///     &lt;GroveStack Gap="0.5rem"&gt;
    ///         &lt;GroveSkeleton Style="width: 200px; height: 16px;" /&gt;
    ///         &lt;GroveSkeleton Style="width: 150px; height: 16px;" /&gt;
    ///     &lt;/GroveStack&gt;
    /// &lt;/GroveStack&gt;
    /// </code>
    /// Image card skeleton:
    /// <code>
    /// &lt;GroveCard Style="width: 300px;"&gt;
    ///     &lt;GroveSkeleton Variant="SkeletonVariant.Rectangle" Style="width: 100%; height: 200px;" Animation="SkeletonAnimation.Wave" /&gt;
    ///     &lt;GroveSkeleton Style="width: 80%; height: 24px; margin-top: 1rem;" /&gt;
    ///     &lt;GroveSkeleton Style="width: 60%; height: 16px; margin-top: 0.5rem;" /&gt;
    /// &lt;/GroveCard&gt;
    /// </code>
    /// </example>
    public partial class GroveSkeleton : GroveComponent
    {
        /// <summary>
        /// Gets or sets the shape variant of the skeleton placeholder.
        /// Text creates horizontal bars, Circle creates circular shapes, Rectangle creates rectangular blocks.
        /// </summary>
        /// <value>The skeleton shape variant. Default is <see cref="SkeletonVariant.Text"/>.</value>
        [Parameter]
        public SkeletonVariant Variant { get; set; } = SkeletonVariant.Text;

        /// <summary>
        /// Gets or sets the animation effect applied to the skeleton placeholder.
        /// None (static), Pulse (fade in/out), or Wave (shimmer effect moving across).
        /// </summary>
        /// <value>The animation type. Default is <see cref="SkeletonAnimation.None"/>.</value>
        [Parameter]
        public SkeletonAnimation Animation { get; set; } = SkeletonAnimation.None;

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return ClassList.Create("rz-skeleton")
                           .Add($"rz-skeleton-{Variant.ToString().ToLowerInvariant()}")
                           .Add($"rz-skeleton-{Animation.ToString().ToLowerInvariant()}", Animation != SkeletonAnimation.None)
                           .ToString();
        }

        /// <summary>
        /// Gets the final style string including component-specific styles.
        /// </summary>
        /// <returns>The style string.</returns>
        protected string? GetStyle()
        {
            return Style;
        }
    }
} 