using Crest.Workflows.Api.Client.Resources.Package.Models;
using Refit;

namespace Crest.Workflows.Api.Client.Resources.Package.Contracts;

/// <summary>
/// A client for working with packages.
/// </summary>
public interface IPackageApi
{
    /// <summary>
    /// Gets the installed package version of Crest.Workflows.
    /// </summary>
    [Get("/package/version")]
    Task<PackageVersionResponse> GetAsync(CancellationToken cancellationToken = default);   
}