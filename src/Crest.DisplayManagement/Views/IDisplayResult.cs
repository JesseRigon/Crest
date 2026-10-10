using Crest.DisplayManagement.Handlers;

namespace Crest.DisplayManagement.Views;

public interface IDisplayResult
{
    Task ApplyAsync(BuildDisplayContext context);
    Task ApplyAsync(BuildEditorContext context);
}
