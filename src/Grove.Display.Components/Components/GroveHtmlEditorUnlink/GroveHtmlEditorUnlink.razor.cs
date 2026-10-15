using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which removes a link.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorUnlink /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorUnlink : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "unlink";


        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Remove link"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorUnlink_Title)); set => title = value; }
    }
}
