using Microsoft.Extensions.DependencyInjection;

namespace Crest.Components.Modules;

/// <summary>
/// Where the admin client runs: <see cref="ApiBaseAddress"/> is where Crest's own
/// <c>api/crest/*</c> calls resolve (the admin document base; the server maps it to the
/// tenant); <see cref="TenantBaseAddress"/> is the tenant root, URL prefix included, for a
/// module's own endpoints mapped outside <c>api/crest</c>. Registered by the admin client.
/// </summary>
public sealed record CrestClientEnvironment(Uri ApiBaseAddress, Uri TenantBaseAddress);

/// <summary>
/// Crest's antiforgery request token for the signed-in session, for a module's own
/// HTTP clients: Crest APIs and module APIs behind the same cookie validate it on every
/// unsafe request. Crest's own API client adds it by itself.
/// </summary>
public interface ICrestAntiforgery
{
    Task<CrestAntiforgeryRequestToken> GetTokenAsync(CancellationToken cancellationToken = default);
}

public sealed record CrestAntiforgeryRequestToken(string HeaderName, string RequestToken);

/// <summary>
/// Marks a component in a module's client assembly as one the module's JavaScript renders
/// (a custom element, a JS root component): the admin client registers it at startup,
/// under <see cref="Identifier"/> with the JS <see cref="Initializer"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CrestJSComponentAttribute(string identifier, string initializer) : Attribute
{
    public string Identifier { get; } = identifier;
    public string Initializer { get; } = initializer;
}
