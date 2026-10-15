using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A GroveHtmlEditor tool which sets the text color of the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor @bind-Value=@html&gt;
    ///  &lt;GroveHtmlEditorColor /&gt;
    /// &lt;/GroveHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorColor : GroveHtmlEditorColorBase
    {
        /// <inheritdoc />
        protected override string CommandName => "foreColor";

        /// <summary>
        /// Specifies the default text color. Set to <c>"rgb(255, 0, 0)"</c> by default;
        /// </summary>
        [Parameter]
        public override string Value { get; set; } = "rgb(255, 0, 0)";
        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Text color"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorColor_Title)); set => title = value; }
    }
}
