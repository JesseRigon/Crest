using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which inserts an ordered list (<c>ol</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorOrderedList /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorOrderedList : GroveHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "insertOrderedList";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Ordered list"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorOrderedList_Title)); set => title = value; }
    }
}
