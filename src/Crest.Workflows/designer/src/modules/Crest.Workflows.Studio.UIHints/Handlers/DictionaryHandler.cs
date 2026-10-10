using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Provides a handler for the <see cref="InputUIHints.Dictionary"/> UI hint.
/// </summary>
public class DictionaryHandler : IUIHintHandler
{
    /// <inheritdoc />
    public bool GetSupportsUIHint(string uiHint) => uiHint is InputUIHints.Dictionary;

    /// <inheritdoc />
    public string UISyntax => WellKnownSyntaxNames.Object;

    /// <inheritdoc />
    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(Dictionary));
            builder.AddAttribute(1, nameof(Dictionary.EditorContext), context);
            builder.CloseComponent();
        };
    }
}