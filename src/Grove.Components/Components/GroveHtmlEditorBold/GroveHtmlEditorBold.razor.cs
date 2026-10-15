using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which bolds the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorBold /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!";
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorBold : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "bold";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Bold"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorBold_Title)); set => title = value; }

        /// <summary>
        /// Specifies the shortcut for the command. Set to <c>"Ctrl+B"</c> by default.
        /// </summary>
        [Parameter]
        public override string? Shortcut { get; set; } = "Ctrl+B";
    }
}
