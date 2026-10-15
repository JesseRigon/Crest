using Grove.Workflows.Api.Client.Resources.Package.Models;
using Refit;

namespace Grove.Workflows.Api.Client.Resources.Package.Contracts;

/// <summary>
/// A client for working with packages.
/// </summary>
public interface IPackageApi
{
    /// <summary>
    /// Gets the installed package version of Grove.Workflows.
    /// </summary>
    [Get("/package/version")]
    Task<PackageVersionResponse> GetAsync(CancellationToken cancellationToken = default);   
}