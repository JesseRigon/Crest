namespace Grove.Api;

/// <summary>
/// The tenant's API endpoint registry (docs/MVC/api.md › The seam): what the tenant serves, for the
/// OpenAPI document, the gate and the conformance tests.
/// </summary>
public interface IApiEndpointRegistry
{
    /// <summary>The declared layer: the contributors mapped in this tenant, overrides applied.</summary>
    IReadOnlyList<ApiRegistration> Declared { get; }

    /// <summary>The defined layer: entries from data, as last loaded.</summary>
    IReadOnlyList<DefinedApiEntry> Defined { get; }

    /// <summary>
    /// Contributors whose endpoints could not be built in this tenant. Each is left out of the
    /// tenant's endpoints (the others keep serving), logged as an error with its stack trace, and
    /// reported by the <c>api-registry</c> health check.
    /// </summary>
    IReadOnlyList<ApiRegistrationFailure> Failures { get; }
}

/// <summary>A contributor left out because building its endpoints threw.</summary>
public sealed record ApiRegistrationFailure(string Name, string FeatureId, string ContributorType, string Error, Exception Exception);

/// <summary>A declared API: its contributor and the routes it mapped.</summary>
public sealed record ApiRegistration(string Name, string FeatureId, string RoutePrefix, IReadOnlyList<ApiRoute> Routes);

/// <summary>One route of a registration.</summary>
public sealed record ApiRoute(string Pattern, IReadOnlyList<string> HttpMethods);
