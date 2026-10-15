using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Models;
using Grove.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Provides a handler for the <see cref="InputUIHints.JsonEditor"/> UI hint.
/// </summary>
public class JsonEditorHandler : IUIHintHandler
{
    /// <inheritdoc />
    public bool GetSupportsUIHint(string uiHint) => uiHint is InputUIHints.JsonEditor;

    /// <inheritdoc />
    public string UISyntax => WellKnownSyntaxNames.Literal;

    /// <inheritdoc />
    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(Json));
            builder.AddAttribute(1, nameof(Json.EditorContext), context);
            builder.CloseComponent();
        };
    }
}