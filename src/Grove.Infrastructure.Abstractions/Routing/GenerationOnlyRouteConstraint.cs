using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Grove.Routing;

/// <summary>
/// A route that only generates URLs: it never matches an incoming request, so another endpoint
/// (a Blazor page) serves the path while links are still built from the route's values.
/// </summary>
public sealed class GenerationOnlyRouteConstraint : IRouteConstraint
{
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection) =>
        routeDirection == RouteDirection.UrlGeneration;
}
