using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Crest.Api;

/// <summary>
/// The tenant's API endpoint registry (docs/MVC/api.md › The seam). The declared layer is filled
/// when the tenant's pipeline maps its contributors; the controller list is read from the tenant's
/// MVC action endpoints under <c>api/</c>; the defined layer is the tenant's
/// <see cref="DefinedApiEndpointDataSource"/>.
/// </summary>
internal sealed class ApiEndpointRegistry(DefinedApiEndpointDataSource defined) : IApiEndpointRegistry
{
    private IReadOnlyList<IApiContributor> _contributors = [];
    private IReadOnlyList<ApiRegistrationFailure> _failures = [];
    private IEndpointRouteBuilder? _routes;
    private IReadOnlyList<ApiRegistration>? _declared;
    private IReadOnlyList<ApiControllerRegistration>? _controllers;

    public IReadOnlyList<ApiRegistration> Declared => _declared ??= ReadDeclared();

    public IReadOnlyList<ApiControllerRegistration> Controllers => _controllers ??= ReadControllers();

    public IReadOnlyList<DefinedApiEntry> Defined => defined.Entries;

    public IReadOnlyList<ApiRegistrationFailure> Failures => _failures;

    internal void SetDeclared(IReadOnlyList<IApiContributor> contributors, IReadOnlyList<ApiRegistrationFailure> failures, IEndpointRouteBuilder routes)
    {
        _contributors = contributors;
        _failures = failures;
        _routes = routes;
        _declared = null;
        _controllers = null;
    }

    private IEnumerable<RouteEndpoint> RouteEndpoints() =>
        _routes?.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>() ?? [];

    private static ApiRoute ToRoute(RouteEndpoint endpoint) => new(
        endpoint.RoutePattern.RawText ?? string.Empty,
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.ToArray() ?? []);

    private IReadOnlyList<ApiRegistration> ReadDeclared()
    {
        var routesByName = RouteEndpoints()
            .Select(endpoint => (endpoint, contributor: endpoint.Metadata.GetMetadata<ApiContributorMetadata>()))
            .Where(pair => pair.contributor is not null)
            .ToLookup(pair => pair.contributor!.Name, pair => ToRoute(pair.endpoint), StringComparer.OrdinalIgnoreCase);

        return _contributors
            .Select(contributor => new ApiRegistration(contributor.Name, contributor.FeatureId, contributor.RoutePrefix, routesByName[contributor.Name].ToArray()))
            .ToArray();
    }

    private IReadOnlyList<ApiControllerRegistration> ReadControllers()
    {
        return RouteEndpoints()
            .Select(endpoint => (endpoint, action: endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(pair => pair.action is not null
                && (pair.endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/').StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            .GroupBy(pair => pair.action!.ControllerTypeInfo.FullName ?? pair.action.ControllerName)
            .Select(group => new ApiControllerRegistration(
                group.First().action!.ControllerName,
                group.First().action!.ControllerTypeInfo.Assembly.GetName().Name ?? string.Empty,
                group.Select(pair => ToRoute(pair.endpoint)).ToArray()))
            .OrderBy(registration => registration.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
