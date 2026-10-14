using Microsoft.AspNetCore.Http;

namespace Crest.Api;

/// <summary>
/// One endpoint of the registry's defined layer: an entry that comes from data (a workflow's HTTP
/// endpoint, a generated operation endpoint), changes at runtime and is served through the
/// tenant's dynamic endpoint data source (docs/MVC/api.md P15).
/// </summary>
/// <param name="Id">Stable id of the entry within its source.</param>
/// <param name="Source">The <see cref="IDefinedApiSource.Name"/> that produced it.</param>
/// <param name="RoutePattern">The route template, absolute from the tenant root (e.g. <c>/workflows/orders/{id}</c>).</param>
/// <param name="HttpMethods">Methods the entry answers; empty answers every method.</param>
/// <param name="Handler">The request delegate.</param>
/// <param name="Metadata">Endpoint metadata added to the entry (permission, tags, ...).</param>
/// <param name="Order">The route order among endpoints that match the same request; lower wins.</param>
public sealed record DefinedApiEntry(
    string Id,
    string Source,
    string RoutePattern,
    IReadOnlyList<string> HttpMethods,
    RequestDelegate Handler,
    IReadOnlyList<object>? Metadata = null,
    int Order = 0);

/// <summary>
/// A source of defined entries, registered in the tenant container. The server reads every source
/// when the tenant's pipeline is built and again after <see cref="IDefinedApiInvalidator.Invalidate"/>.
/// Sources are resolved in a tenant scope, so what they return is the tenant's own data only.
/// </summary>
public interface IDefinedApiSource
{
    /// <summary>The source name; entries carry it as <see cref="DefinedApiEntry.Source"/>.</summary>
    string Name { get; }

    Task<IReadOnlyList<DefinedApiEntry>> GetEntriesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tells the tenant's defined layer that a source's entries changed (a publish). The layer reloads
/// the entries in the background and swaps the tenant's endpoints when they are read.
/// </summary>
public interface IDefinedApiInvalidator
{
    void Invalidate(string source);
}
