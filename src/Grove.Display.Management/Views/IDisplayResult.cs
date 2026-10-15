using Grove.Display.Management.Handlers;

namespace Grove.Display.Management.Views;

public interface IDisplayResult
{
    Task ApplyAsync(BuildDisplayContext context);
}
