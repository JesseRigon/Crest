using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Provides a handler for the <see cref="InputUIHints.CodeEditor"/> UI hint.
/// </summary>
public class CodeEditorHandler : IUIHintHandler
{
    /// <inheritdoc />
    public bool GetSupportsUIHint(string uiHint) => uiHint is InputUIHints.CodeEditor;

    /// <inheritdoc />
    public string UISyntax => WellKnownSyntaxNames.Literal;

    /// <inheritdoc />
    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(Code));
            builder.AddAttribute(1, nameof(Code.EditorContext), context);
            builder.CloseComponent();
        };
    }
}