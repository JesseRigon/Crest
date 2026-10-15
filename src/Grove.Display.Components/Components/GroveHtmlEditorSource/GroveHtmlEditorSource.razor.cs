using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Grove.Display.Components.Primitives
{
    /// <summary>
    /// A tool which switches between rendered and source views in <see cref="GroveHtmlEditor" />.
    /// </summary>
    public partial class GroveHtmlEditorSource
    {

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"View source"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(GroveStrings.HtmlEditorSource_Title)); set => title = value; }

    }
}
