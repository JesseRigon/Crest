using Crest.Workflows.Api.Client.Resources.Features.Models;
using Crest.Workflows.Api.Client.Shared.Models;
using Refit;

namespace Crest.Workflows.Api.Client.Resources.Features.Contracts;

/// <summary>
/// A client for working with features.
/// </summary>
public interface IFeaturesApi
{
    /// <summary>
    /// Gets the specified feature.
    /// </summary>
    [Get("/features/installed/{fullName}")]
    Task<FeatureDescriptor> GetAsync(string fullName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the specified feature.
    /// </summary>
    [Get("/features/installed")]
    Task<ListResponse<FeatureDescriptor>> ListAsync(CancellationToken cancellationToken = default);
}