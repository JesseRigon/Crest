using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Provides a handler for the <see cref="InputUIHints.MultiText"/> UI hint.
/// </summary>
public class MultiTextHandler : IUIHintHandler
{
    /// <inheritdoc />
    public bool GetSupportsUIHint(string uiHint) => uiHint is InputUIHints.MultiText;

    /// <inheritdoc />
    public string UISyntax => WellKnownSyntaxNames.Object;

    /// <inheritdoc />
    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(MultiText));
            builder.AddAttribute(1, nameof(MultiText.EditorContext), context);
            builder.CloseComponent();
        };
    }
}