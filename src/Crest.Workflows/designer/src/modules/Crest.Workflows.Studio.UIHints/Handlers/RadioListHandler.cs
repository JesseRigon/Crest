using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Represents the radio list handler.
/// </summary>
public class RadioListHandler : IUIHintHandler
{
    /// <summary>
    /// Provides the get supports uihint.
    /// </summary>
    public bool GetSupportsUIHint(string uiHint) => uiHint == InputUIHints.RadioList;

    /// <summary>
    /// Provides the uisyntax.
    /// </summary>
    public string UISyntax => WellKnownSyntaxNames.Literal;

    /// <summary>
    /// Performs the display input editor operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The result of the operation.</returns>
    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(RadioList));
            builder.AddAttribute(1, nameof(RadioList.EditorContext), context);
            builder.CloseComponent();
        };
    }
}