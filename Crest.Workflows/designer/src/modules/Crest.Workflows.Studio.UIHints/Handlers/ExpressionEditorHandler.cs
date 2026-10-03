using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Provides a handler for the <see cref="InputUIHints.ExpressionEditor"/> UI hint.
/// </summary>
public class ExpressionEditorHandler : IUIHintHandler
{
    /// <inheritdoc />
    public bool GetSupportsUIHint(string uiHint) => uiHint is InputUIHints.ExpressionEditor;

    /// <inheritdoc />
    public string UISyntax => "JavaScript";

    /// <inheritdoc />
    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(ExpressionEditor));
            builder.AddAttribute(1, nameof(ExpressionEditor.EditorContext), context);
            builder.CloseComponent();
        };
    }
}