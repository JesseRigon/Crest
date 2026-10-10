using Crest.Workflows.Api.Client.Shared;

namespace Crest.Workflows.Api.Client.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="HttpResponseMessage"/> class.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Gets the workflow instance ID from the response.
    /// </summary>
    public static string? GetWorkflowInstanceId(this HttpResponseMessage response) => response.Headers.TryGetValues(HeaderNames.WorkflowInstanceId, out var values) ? values.FirstOrDefault() : default;
}