using Grove.DisplayManagement.Handlers;

namespace Grove.DisplayManagement.Views;

public interface IDisplayResult
{
    Task ApplyAsync(BuildDisplayContext context);
    Task ApplyAsync(BuildEditorContext context);
}
