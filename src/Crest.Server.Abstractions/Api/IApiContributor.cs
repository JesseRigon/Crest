using Microsoft.AspNetCore.Routing;

namespace Crest.Api;

/// <summary>
/// Declares a module's API endpoints (docs/MVC/api.md › The seam). Registered from the startup of
/// the feature that owns the API (<see cref="ApiServiceCollectionExtensions.AddApiContributor{TContributor}"/>),
/// so a tenant that has not enabled the feature never maps the endpoints. The server maps every
/// contributor registered in the tenant into a route group that carries the hosting plumbing:
/// the prefix, antiforgery, the error contract and the OpenAPI tag.
/// </summary>
public interface IApiContributor
{
    /// <summary>
    /// The registry name of this API: unique per tenant, the OpenAPI tag. A contributor registered
    /// later under the same name replaces an earlier one (a downstream module's override).
    /// </summary>
    string Name { get; }

    /// <summary>The feature whose enablement turns these endpoints on.</summary>
    string FeatureId { get; }

    /// <summary>
    /// The group prefix the endpoints are mapped under: <see cref="ApiRoutes.Prefix"/> unless the API
    /// keeps a path of its own (protocols, the workflow engine's management API).
    /// </summary>
    string RoutePrefix { get; }

    /// <summary>Maps the contributor's endpoints into the group the server hands it.</summary>
    void Map(IEndpointRouteBuilder group);
}

/// <summary>
/// The usual contributor: endpoints under <see cref="ApiRoutes.Prefix"/>.
/// </summary>
public abstract class ApiContributor : IApiContributor
{
    public abstract string Name { get; }

    public abstract string FeatureId { get; }

    public virtual string RoutePrefix => ApiRoutes.Prefix;

    public abstract void Map(IEndpointRouteBuilder group);
}
