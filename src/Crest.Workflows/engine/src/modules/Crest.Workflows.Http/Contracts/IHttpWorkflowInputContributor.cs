using Microsoft.AspNetCore.Http;

namespace Crest.Workflows.Http;

/// <summary>
/// Adds entries to the workflow input of a run started or resumed by <see cref="Middleware.HttpWorkflowsMiddleware"/>,
/// after the request is authorized and before the workflow runs. The host registers one to
/// carry what every run must know about the request, such as the acting caller.
/// </summary>
public interface IHttpWorkflowInputContributor
{
    Task ContributeAsync(HttpContext httpContext, IDictionary<string, object> input, CancellationToken cancellationToken = default);
}
