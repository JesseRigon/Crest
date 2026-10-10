using Crest.Workflows.Http.Middleware;
using Microsoft.AspNetCore.Builder;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

/// <summary>
/// Adds extension methods to <see cref="IApplicationBuilder"/> related to workflow middleware components.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Installs the <see cref="HttpWorkflowsMiddleware"/> component.
    /// </summary>
    public static IApplicationBuilder UseWorkflows(this IApplicationBuilder app) => app.UseMiddleware<HttpWorkflowsMiddleware>();
}