using Crest.Workflows.Api.Middleware;
using Microsoft.AspNetCore.Builder;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

/// <summary>
/// Provides extensions to <see cref="IApplicationBuilder"/>.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds middleware that handles JSON serialization errors.
    /// </summary>
    public static IApplicationBuilder UseJsonSerializationErrorHandler(this IApplicationBuilder app) => app.UseMiddleware<JsonSerializationErrorHandlerMiddleware>();
}