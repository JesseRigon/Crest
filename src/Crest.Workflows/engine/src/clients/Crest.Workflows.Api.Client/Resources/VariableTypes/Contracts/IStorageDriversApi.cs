using Crest.Workflows.Api.Client.Resources.VariableTypes.Responses;
using Refit;

namespace Crest.Workflows.Api.Client.Resources.VariableTypes.Contracts;

/// <summary>
/// Represents a client for the variable types API.
/// </summary>
public interface IVariableTypesApi
{
    /// <summary>
    /// Lists variable types.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response containing the variable types.</returns>
    [Get("/descriptors/variables")]
    Task<ListVariableTypesResponse> ListAsync(CancellationToken cancellationToken = default);
}