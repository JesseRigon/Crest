using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Represents the date time picker handler.
/// </summary>
public class DateTimePickerHandler : IUIHintHandler
{
    /// <summary>
    /// Provides the get supports uihint.
    /// </summary>
    public bool GetSupportsUIHint(string uiHint) => uiHint == InputUIHints.DateTimePicker;

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
            builder.OpenComponent(0, typeof(DateTimePicker));
            builder.AddAttribute(1, nameof(DateTimePicker.EditorContext), context);
            builder.CloseComponent();
        };
    }
}