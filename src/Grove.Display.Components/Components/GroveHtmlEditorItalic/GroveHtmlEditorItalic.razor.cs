using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which makes the selection italic.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorItalic /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!";
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorItalic : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "italic";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Italic"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorItalic_Title)); set => title = value; }

        /// <summary>
        /// Specifies the shortcut for the command. Set to <c>"Ctrl+I"</c> by default.
        /// </summary>
        [Parameter]
        public override string? Shortcut { get; set; } = "Ctrl+I";
    }
}
