using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Adds a custom color to <see cref="GroveHtmlEditorColor" />.
    /// </summary>
    /// <example>
    /// <code>
    ///  &lt;GroveHtmlEditorColor &gt;
    ///     &lt;GroveHtmlEditorColorItem Value="red" /&gt;
    ///     &lt;GroveHtmlEditorColorItem Value="green" /&gt;
    ///  &lt;/GroveHtmlEditorColor &gt;
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorColorItem
    {
        /// <summary>
        /// The custom color to add.
        /// </summary>
        [Parameter]
        public string? Value { get; set; }
    }
}
