using System.Text.Json;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Models;
using Crest.Workflows.Resilience;
using Crest.Workflows.Resilience.Entities;
using Crest.Workflows.Resilience.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;

namespace Crest.Workflows.Api.Endpoints.Resilience;

/// <summary>The resilience feature's reads: the strategy catalog, an activity's retry attempts, and the response simulator.</summary>
public static class ResilienceEndpoints
{
    private static readonly TimeSpan s_simulationSessionExpiry = TimeSpan.FromMinutes(15);

    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/resilience/strategies", ListStrategiesAsync).RequireAuthorization(EngineApiPolicies.ReadResilienceStrategies);
        routes.MapGet("/resilience/retries/{activityInstanceId}", ListRetriesAsync).RequireAuthorization(EngineApiPolicies.ReadResilienceRetries);
        routes.MapGet("/simulate-response", SimulateResponse).AllowAnonymous();
    }

    private static async Task<IResult> ListStrategiesAsync(
        HttpContext context,
        [FromServices] IResilienceStrategyCatalog catalog,
        [FromServices] ResilienceStrategySerializer serializer,
        CancellationToken cancellationToken)
    {
        var strategies = (await catalog.ListAsync(cancellationToken)).ToList();
        return EngineResults.Json(context, new ListResponse<IResilienceStrategy>(strategies), serializer.SerializerOptions);
    }

    private static async Task<IResult> ListRetriesAsync(
        HttpContext context,
        [FromRoute] string activityInstanceId,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        [FromServices] IRetryAttemptReader reader,
        CancellationToken cancellationToken)
    {
        var pageArgs = skip == null && take == null ? null : PageArgs.FromRange(skip, take);
        var page = await reader.ReadAttemptsAsync(activityInstanceId, pageArgs, cancellationToken);
        return EngineResults.Json(context, new PagedListResponse<RetryAttemptRecord>(page));
    }

    /// <summary>Answers the next status code of the session's sequence (<c>codes</c>, a JSON array; 429, 503, 200 by default).</summary>
    private static IResult SimulateResponse(
        HttpContext context,
        [FromQuery] string? sessionId,
        [FromQuery] string? codes,
        [FromServices] IMemoryCache memoryCache)
    {
        int[] defaultCodes = [429, 503, 200];
        var sequence = string.IsNullOrWhiteSpace(codes) ? defaultCodes : JsonSerializer.Deserialize<int[]>(codes)!;
        var cacheKey = $"status-simulation-session-{sessionId ?? "default"}";
        var nextIndex = memoryCache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = s_simulationSessionExpiry;
            return 0;
        });

        var currentCode = nextIndex < sequence.Length ? sequence[nextIndex] : sequence[^1];
        var message = StatusCodeMessages.Lookup.TryGetValue(currentCode, out var reason) ? reason : $"Status Code {currentCode}";

        if (nextIndex + 1 >= sequence.Length)
        {
            memoryCache.Remove(cacheKey);
        }
        else
        {
            memoryCache.Set(cacheKey, nextIndex + 1, new MemoryCacheEntryOptions { SlidingExpiration = s_simulationSessionExpiry });
        }

        return EngineResults.Json(context, new SimulatedResponse(message), currentCode);
    }
}

public record SimulatedResponse(string Message);
