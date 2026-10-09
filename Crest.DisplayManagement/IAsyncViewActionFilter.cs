using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crest.DisplayManagement;

public interface IAsyncViewActionFilter : IAsyncActionFilter, IAsyncPageFilter
{
    Task OnActionExecutionAsync(ActionContext context);
}
