using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// Adds a custom font to a <see cref="GroveHtmlEditorFontName" />.
    /// </summary>
    /// <example>
    /// <code>
    ///  &lt;GroveHtmlEditorFontName&gt;
    ///  &lt;GroveHtmlEditorFontNameItem Text="Times New Roman" Value='"Times New Roman"' /&gt;
    ///  &lt;/GroveHtmlEditorFontName&gt;
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorFontNameItem
    {
        /// <summary>
        /// The name of the font e.g. <c>"Times New Roman"</c>.
        /// </summary>
        [Parameter]
        public string? Text { get; set; }

        /// <summary>
        /// The CSS value of the font. Use quotes if it contains spaces.
        /// </summary>
        [Parameter]
        public string? Value { get; set; }

        /// <summary>
        /// The GroveHtmlEditorFontName tool which this tool belongs to.
        /// </summary>
        [CascadingParameter]
        public GroveHtmlEditorFontName? Parent { get; set; }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            Parent?.AddFont(this);
        }
    }
}
