using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives
{
    /// <summary>
    /// A custom tool in GroveHtmlEditor
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;GroveHtmlEditor Execute=@OnExecute&gt;
    ///   &lt;GroveHtmlEditorCustomTool CommandName="InsertToday" Icon="today" Title="Insert today" /&gt;
    /// &lt;/GroveHtmlEditor&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    ///   async Task OnExecute(HtmlEditorExecuteEventArgs args)
    ///   {
    ///     if (args.CommandName == "InsertToday")
    ///     {
    ///       await args.Editor.ExecuteCommandAsync(HtmlEditorCommands.InsertHtml, DateTime.Today.ToLongDateString());
    ///     }
    ///  }
    /// </code>
    /// </example>
    public partial class GroveHtmlEditorCustomTool
    {
        /// <summary>
        /// Determines if the tools is visible.
        /// </summary>
        [Parameter]
        public bool Visible { get; set; } = true;

        /// <summary>
        /// Specifies the icon of the tool. Set to <c>"settings"</c> by default.
        /// </summary>
        [Parameter]
        public string Icon { get; set; } = "settings";

        /// <summary>
        /// Gets or sets the icon color.
        /// </summary>
        /// <value>The icon color.</value>
        [Parameter]
        public string? IconColor { get; set; }

        /// <summary>
        /// Specifies the modes that this tool will be enabled in.
        /// </summary>
        [Parameter]
        public HtmlEditorMode EnabledModes { get; set; } = HtmlEditorMode.Design;

        /// <summary>
        /// The template of the tool. Use to render a custom tool.
        /// </summary>
        [Parameter]
        public RenderFragment<GroveHtmlEditor>? Template { get; set; }

        /// <summary>
        /// Specifies whether the tool is selected.
        /// </summary>
        [Parameter]
        public bool Selected { get; set; }

        /// <summary>
        /// Specifies whether the tool is disabled.
        /// </summary>
        [Parameter]
        public bool Disabled { get; set; }

        /// Specifies the name of the command. It is available as <see cref="HtmlEditorExecuteEventArgs.CommandName" /> when
        /// <see cref="GroveHtmlEditor.Execute" /> is raised.
        [Parameter]
        public string? CommandName { get; set; }

        /// <summary>
        /// The GroveHtmlEditor component which this tool is part of.
        /// </summary>
        [CascadingParameter]
        public GroveHtmlEditor? Editor { get; set; }

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool.
        /// </summary>
        [Parameter]
        public string? Title { get; set; }

        async Task OnClick()
        {
            if (Editor != null && CommandName != null)
            {
                await Editor.OnExecuteAsync(CommandName);
            }
        }
    }
}
