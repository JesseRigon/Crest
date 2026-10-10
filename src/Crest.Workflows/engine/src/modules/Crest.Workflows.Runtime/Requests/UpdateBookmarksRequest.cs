using Crest.Workflows.Helpers;
using Crest.Workflows.Models;

namespace Crest.Workflows.Runtime.Requests;

/// <summary>
/// Published when bookmarks needs to be updated.
/// </summary>
/// <param name="WorkflowExecutionContext">The workflow execution context.</param>
/// <param name="Diff">A diff of the bookmarks.</param>
/// <param name="CorrelationId">The correlation ID, if any.</param>
public record UpdateBookmarksRequest(WorkflowExecutionContext WorkflowExecutionContext, Diff<Bookmark> Diff, string? CorrelationId = default);