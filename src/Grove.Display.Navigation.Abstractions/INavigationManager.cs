using Microsoft.AspNetCore.Http;

namespace Grove.Display.Navigation;

public interface INavigationManager
{
    /// <summary>Builds the named menu for the request: authorized for its user, with each item's href.</summary>
    Task<IEnumerable<MenuItem>> BuildMenuAsync(string name, HttpContext httpContext);
}
