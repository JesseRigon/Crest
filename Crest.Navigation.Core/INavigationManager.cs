using Microsoft.AspNetCore.Mvc;

namespace Crest.Navigation;

public interface INavigationManager
{
    Task<IEnumerable<MenuItem>> BuildMenuAsync(string name, ActionContext context);
}
