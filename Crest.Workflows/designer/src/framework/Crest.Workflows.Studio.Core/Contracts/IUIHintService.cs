namespace Crest.Workflows.Studio.Contracts;

/// <summary>
/// Defines the contract for uihint service.
/// </summary>
public interface IUIHintService
{
    IUIHintHandler GetHandler(string uiHint);
    //RenderFragment DisplayInputEditor(DisplayInputEditorContext context);
}