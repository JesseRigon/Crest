using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.UIHints.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Workflows.Studio.UIHints.Handlers;

/// <summary>
/// Provides a handler for the <see cref="InputUIHints.CheckList"/> UI hint.
/// </summary>
public class CheckListHandler : IUIHintHandler
{
    /// <inheritdoc />
    public bool GetSupportsUIHint(string uiHint) => uiHint is InputUIHints.CheckList;

    /// <inheritdoc />
    public string UISyntax => WellKnownSyntaxNames.Object;

    /// <inheritdoc />
    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(CheckList));
            builder.AddAttribute(1, nameof(CheckList.EditorContext), context);
            builder.CloseComponent();
        };
    }
}