using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// Adds a custom color to <see cref="GroveHtmlEditorBackground" />.
    /// </summary>
    /// <example>
    /// <code>
    ///  &lt;GroveHtmlEditorBackground &gt;
    ///     &lt;GroveHtmlEditorBackgroundItem Value="red" /&gt;
    ///     &lt;GroveHtmlEditorBackgroundItem Value="green" /&gt;
    ///  &lt;/GroveHtmlEditorBackground &gt;
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorBackgroundItem
    {
        /// <summary>
        /// The custom color to add.
        /// </summary>
        [Parameter]
        public string? Value { get; set; }
    }
}
