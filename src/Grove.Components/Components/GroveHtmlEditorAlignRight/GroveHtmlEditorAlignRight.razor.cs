using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which aligns the selection to the right.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorAlignRight /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorAlignRight : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "justifyRight";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Align right"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorAlignRight_Title)); set => title = value; }
    }
}
