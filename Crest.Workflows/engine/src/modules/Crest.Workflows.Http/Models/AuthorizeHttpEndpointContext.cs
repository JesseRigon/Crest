using Crest.Workflows.Activities;
using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Http;

/// <summary>
/// Represents the context for authorizing an HTTP endpoint.
/// </summary>
public record AuthorizeHttpEndpointContext(HttpContext HttpContext, Workflow Workflow, string? Policy = default);