using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Grove.Mvc.Routing;

public interface IAreaControllerRouteMapper
{
    int Order { get; }
    bool TryMapAreaControllerRoute(IEndpointRouteBuilder routes, ControllerActionDescriptor descriptor);
}
