using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which underlines the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorUnderline /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!";
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorUnderline : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "underline";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Underline"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorUnderline_Title)); set => title = value; }

        /// <summary>
        /// Specifies the shortcut for the command. Set to <c>"Ctrl+U"</c> by default.
        /// </summary>
        [Parameter]
        public override string? Shortcut { get; set; } = "Ctrl+U";
    }
}
