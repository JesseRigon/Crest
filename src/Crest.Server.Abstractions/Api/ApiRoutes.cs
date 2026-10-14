namespace Crest.Api;

/// <summary>
/// Route conventions of the API endpoint registry.
/// </summary>
public static class ApiRoutes
{
    /// <summary>The prefix every declared API is mapped under unless its contributor names its own.</summary>
    public const string Prefix = "api/crest";
}

/// <summary>
/// Endpoint metadata that exempts an endpoint of a registry route group from the server's
/// antiforgery check: for endpoints a browser cannot be made to call with the user's cookie
/// (an external callback authenticated by its own signature, a token endpoint called before the
/// session has one). Add it with <see cref="ApiEndpointConventionExtensions.SkipApiAntiforgery{TBuilder}"/>.
/// </summary>
public sealed class SkipApiAntiforgeryMetadata
{
    public static readonly SkipApiAntiforgeryMetadata Instance = new();
}

public static class ApiEndpointConventionExtensions
{
    public static TBuilder SkipApiAntiforgery<TBuilder>(this TBuilder builder)
        where TBuilder : Microsoft.AspNetCore.Builder.IEndpointConventionBuilder
    {
        builder.Add(endpoint => endpoint.Metadata.Add(SkipApiAntiforgeryMetadata.Instance));
        return builder;
    }
}
