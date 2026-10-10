using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Shared.Models;
using Refit;

namespace Crest.Workflows.Api.Client.Resources.Resilience.Contracts;

/// <summary>
/// Provides operations for managing and retrieving resilience strategies within the application.
/// </summary>
public interface IResilienceStrategiesApi
{
    /// <summary>
    /// Retrieves a list of resilience strategies from the resilience strategies API endpoint.
    /// </summary>
    /// <param name="category">The category to filter the strategies by.</param>
    /// <param name="cancellationToken">An optional token to cancel the operation.</param>
    /// <returns>A <see cref="ListResponse{JsonObject}"/> with the list of resilience strategies.</returns>
    [Get("/resilience/strategies")]
    Task<ListResponse<JsonObject>> ListAsync(string category, CancellationToken cancellationToken = default);
}