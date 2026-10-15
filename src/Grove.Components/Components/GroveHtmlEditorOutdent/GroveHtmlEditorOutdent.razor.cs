using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which outdents the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorOutdent /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorOutdent : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "outdent";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Outdent"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorOutdent_Title)); set => title = value; }
    }
}
