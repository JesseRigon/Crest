using Microsoft.AspNetCore.Mvc;

namespace Grove.Navigation;

public interface INavigationManager
{
    Task<IEnumerable<MenuItem>> BuildMenuAsync(string name, ActionContext context);
}
