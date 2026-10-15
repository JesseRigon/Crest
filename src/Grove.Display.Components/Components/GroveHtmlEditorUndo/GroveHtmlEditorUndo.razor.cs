using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which reverts the last edit operation.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorUndo /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorUndo : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "undo";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Undo"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorUndo_Title)); set => title = value; }
    }
}
