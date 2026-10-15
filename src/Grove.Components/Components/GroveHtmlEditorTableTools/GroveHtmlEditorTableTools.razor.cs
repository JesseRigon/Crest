using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A built-in HTML editor toolbar group for table insertion and table commands.
    /// </summary>
    public partial class GroveHtmlEditorTableTools : GroveHtmlEditorButtonBase
    {
        /// <summary>
        /// Gets or sets localizable strings used by the table tools. Falls back to <see cref="GroveHtmlEditor.TableStrings" />.
        /// </summary>
        [Parameter]
        public HtmlEditorTableStrings? TableStrings { get; set; }
    }
}
